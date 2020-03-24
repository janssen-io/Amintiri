namespace Amintiri.UserAccess

module Registration =
    open Domain

    type internal Register = Registration -> Result<Unit, RegistrationError>

    let private validatePasswordConfirmation registration =
        if registration.Password = registration.PasswordConfirmation then Ok registration
        else Error IncorrectPasswordConfirmation

    let private validateUniqueUsername connectionString registration =
        let username = registration.Username
        match Database.findUser connectionString username with
        | Ok None -> Ok registration
        | Ok _ -> Error(DuplicateUsername username)
        | Error exn -> Error(TechnicalError exn)

    let register connectionString: Register =
        validatePasswordConfirmation
        >> Result.bind (validateUniqueUsername connectionString)
        >> Result.map (fun reg -> (reg.Username, HashedPassword.hash reg.Password))
        >> Result.bind (fun (user, pass) -> Database.addUser connectionString (user, pass))
