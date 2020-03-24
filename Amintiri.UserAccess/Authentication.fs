namespace Amintiri.UserAccess

module Authentication =
    open Domain
    open HashedPassword

    let private hash (user: User) (login: Credentials) = hashWithSalt user.Password.Salt login.Password

    let validatePassword connectionString: ValidatePassword =
        function
        | credentials ->
            match Database.findUser connectionString credentials.Username with
            | Error _ -> None
            | Ok None -> None
            | Ok(Some user) ->
                if hash user credentials = user.Password then Some credentials.Username
                else None
