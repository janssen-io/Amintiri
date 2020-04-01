namespace Amintiri.Api

open Npgsql.FSharp
open System.Reflection
open System.Text.RegularExpressions

module internal Migrations =
    type private Marker =
        interface
        end

    let ``1. Create Migrations table`` connection =
        Sql.connect connection
        |> Sql.query """
            CREATE TABLE IF NOT EXISTS "public"."migrations" (
                "id" integer NOT NULL,
                "name" text NOT NULL,
                "createdon" timestamp NOT NULL
            ); """
        |> Sql.executeNonQuery

    let ``2. Create Photos table`` connection =
        Sql.connect connection
        |> Sql.query """
            CREATE TABLE IF NOT EXISTS "public"."photos" (
                "id" uuid NOT NULL,
                "name" text DEFAULT 'Untitled' NOT NULL,
                "path" text NOT NULL
            ); """
        |> Sql.executeNonQuery

    let ``3. Create users table`` connection =
        Sql.connect connection
        |> Sql.query """
            CREATE TABLE IF NOT EXISTS "public"."users" (
                "id" uuid NOT NULL,
                "username" text NOT NULL,
                "password" text NOT NULL,
                "salt" text NOT NULL
            ); """
        |> Sql.executeNonQuery

    let private unsafeStrUntil (s: string) (c: char) =
        let index = s.IndexOf c
        s.Substring(0, index)

    let private get_last_migration connection =
        ``1. Create Migrations table`` connection |> ignore
        Sql.connect connection
        |> Sql.query "SELECT id FROM migrations ORDER BY id DESC LIMIT 1;"
        |> Sql.execute (fun reader -> reader.int "id")
        |> Result.map (List.tryHead)

    let private get_migrations connection =
        let lastMigrationId = get_last_migration connection

        let migrationRegex = new Regex("^(\d+)\.")

        let migrations =
            typeof<Marker>.DeclaringType.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Static)
            |> Array.filter (fun m -> migrationRegex.IsMatch(m.Name))
            |> Array.sortBy (fun m -> int (unsafeStrUntil m.Name '.'))

        match lastMigrationId with
        | Ok None -> Ok migrations
        | Ok(Some lastId) -> Array.skip lastId migrations |> Ok
        | Error exn -> Error exn.Message

    let private executeMigration (m: MethodInfo) connection =
        let migration: Result<int, exn> = m.Invoke(null, [| connection |]) :?> Result<int, exn>
        match migration with
        | Ok _ -> ()
        | Error exn -> failwith (sprintf "Cannot apply migration %s: %s)" m.Name exn.Message)

        let id = unsafeStrUntil m.Name '.' |> int
        let createdon = System.DateTime.UtcNow.ToString("yyyy/MM/dd hh:mm:ss")
        Sql.connect connection
        |> Sql.query (sprintf """
            INSERT
                INTO migrations (id, name, createdon)
                VALUES (%i, '%s', '%s')
        """            id m.Name createdon)
        |> Sql.executeNonQuery
        |> function
        | Ok _ -> ()
        | Error exn -> failwith (sprintf "Error registering migration %i: %s" id exn.Message)

    let execute connection =
        match get_migrations connection with
        | Error msg -> failwith msg
        | Ok ms -> ms |> Array.map (fun m -> executeMigration m connection)
        |> ignore
