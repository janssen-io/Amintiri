namespace Amintiri.UserAccess

module Authentication =
    open Domain
    open HashedPassword

    let private hash (user: User) (login: Credentials) = hashWithSalt user.Password.Salt login.Password

    let internal validatePassword (findUser: Username -> Result<User, QueryError>): ValidatePassword =
        function
        | credentials ->
            match findUser credentials.Username with
            | Ok user ->
                match hash user credentials = user.Password with
                | true -> Ok credentials.Username
                | false -> Error IncorrectPassword
            | Error error ->
                match error with
                | TechnicalError exn -> AuthenticationError.TechnicalError exn |> Error
                | NoResults -> IncorrectUsername |> Error
