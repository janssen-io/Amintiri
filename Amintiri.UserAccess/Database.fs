namespace Amintiri.UserAccess

module internal Database =
    open Domain
    open System

    open Npgsql.FSharp

    type UserId = Guid

    let addUser connectionString (Username user, password: HashedPassword) =
        Sql.connect connectionString
        |> Sql.query "INSERT INTO users (id, username, password, salt) VALUES (@id, @username, @password, @salt)"
        |> Sql.parameters
            [ "id", SqlValue.Uuid <| UserId.NewGuid()
              "username", SqlValue.String user
              "password", SqlValue.String password.Hash
              "salt", SqlValue.String(password.Salt |> fun (Salt salt) -> salt) ]
        |> Sql.executeNonQuery
        |> function
        | Ok _ -> Ok()
        | Error e -> Error(TechnicalError e)

    let findUser connectionString (Username user): Result<User option, exn> =
        Sql.connect connectionString
        |> Sql.query "SELECT * FROM users WHERE username = @name"
        |> Sql.parameters [ "name", SqlValue.String user ]
        |> Sql.execute (fun row ->
            { Username = Username <| row.string "username"
              Password =
                  { Hash = row.string "password"
                    Salt = row.string "salt" |> Salt } })
        |> function
        | Ok [] -> Ok None
        | Ok(dbUser :: []) -> Some dbUser |> Ok
        | Ok _ -> failwithf "Multiple users with username '%s'." user
        | Error exn -> Error exn
