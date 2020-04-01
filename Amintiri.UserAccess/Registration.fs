namespace Amintiri.UserAccess

module Registration =
    open Domain

    type internal Register = Registration -> Result<Unit, RegistrationError>

    let private validatePasswordConfirmation registration =
        if registration.Password = registration.PasswordConfirmation
        then Ok registration
        else Error IncorrectPasswordConfirmation

    let private validateUniqueUsername (findUser: Username -> Result<User, QueryError>) registration =
        let username = registration.Username
        match findUser username with
        | Ok _ -> Error(DuplicateUsername username)
        | Error(TechnicalError exn) -> Error(RegistrationError.TechnicalError exn)
        | Error NoResults -> Ok registration

    let internal register findUser addUser: Register =
        validatePasswordConfirmation
        >> Result.bind (validateUniqueUsername findUser)
        >> Result.map (fun reg -> (reg.Username, HashedPassword.hash reg.Password))
        >> Result.bind addUser
