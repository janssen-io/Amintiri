namespace Amintiri.UserAccess

module Domain =
    type Username = Username of string

    type TextPassword = TextPassword of string

    type Salt = Salt of string

    type HashedPassword =
        { Hash: string
          Salt: Salt }

    type Credentials =
        { Username: Username
          Password: TextPassword }

    type User =
        { Username: Username
          Password: HashedPassword }

    type Registration =
        { Username: Username
          Password: TextPassword
          PasswordConfirmation: TextPassword }

    type AuthenticationError =
        | IncorrectUsername
        | IncorrectPassword
        | TechnicalError of exn

    type RegistrationError =
        | IncorrectPasswordConfirmation
        | DuplicateUsername of Username
        | TechnicalError of exn

    type QueryError =
        | TechnicalError of exn
        | NoResults

    type ValidatePassword = Credentials -> Result<Username, AuthenticationError>

    type Register = Registration -> Result<Unit, RegistrationError>
