namespace Amintiri.Photos

module internal Database =
    open Domain
    open Npgsql.FSharp
    open System

    let insert connectionString (path: Path) (name: string) =
        Sql.connect connectionString
        |> Sql.query "INSERT INTO photos (id, path, name) VALUES (@id, @path, @name)"
        |> Sql.parameters
            [ "id", SqlValue.Uuid <| Guid.NewGuid()
              "path", (Path.unwrap >> SqlValue.String) path
              "name", SqlValue.String name ]
        |> Sql.executeNonQuery

    let list connectionString =
        Sql.connect connectionString
        |> Sql.query "SELECT * FROM photos"
        |> Sql.execute (fun row ->
            { Id = row.uuid "id"
              Path = (row.text "path" |> Path.unsafeCreate)
              Name = row.text "name" })
