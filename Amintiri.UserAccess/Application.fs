namespace Amintiri.UserAccess

module Application =
    open Domain
    open System

    open Npgsql.FSharp

    type UserId = Guid

    let private addUser dbConfig (Username user, password: HashedPassword) =
        Sql.formatConnectionString dbConfig
        |> Sql.connect
        |> Sql.query "INSERT INTO users (id, username, password, salt) VALUES (@id, @username, @password, @salt)"
        |> Sql.parameters
            [ "id", SqlValue.Uuid <| UserId.NewGuid()
              "username", SqlValue.String user
              "password", SqlValue.String password.Hash
              "salt", SqlValue.String(password.Salt |> fun (Salt salt) -> salt) ]
        |> Sql.executeNonQuery
        |> function
        | Ok _ -> Ok()
        | Error e -> Error(RegistrationError.TechnicalError e)

    let private findUser dbConfig (Username user): Result<User, QueryError> =
        Sql.formatConnectionString dbConfig
        |> Sql.connect
        |> Sql.query "SELECT * FROM users WHERE username = @name"
        |> Sql.parameters [ "name", SqlValue.String user ]
        |> Sql.execute (fun row ->
            { Username = Username <| row.string "username"
              Password =
                  { Hash = row.string "password"
                    Salt = row.string "salt" |> Salt } })
        |> function
        | Ok [] -> Error NoResults
        | Ok(dbUser :: []) -> Ok dbUser
        | Ok _ -> failwithf "Multiple users with username '%s'." user // use exceptions if domain constraints are found broken
        | Error exn -> Error(TechnicalError exn)

    let registerUser dbConfig = Registration.register (findUser dbConfig) (addUser dbConfig)

    let authenticateUser dbConfig = Authentication.validatePassword (findUser dbConfig)
