namespace Amintiri.Api.Modules.UserAccess

module Types =
    type Config() =
        member val Issuer = "" with get, set
        member val Audience = "" with get, set
        member val AppKey = "" with get, set

    [<CLIMutable>]
    type RegistrationDto =
        { Username: string
          Password: string
          ConfirmPassword: string }

    [<CLIMutable>]
    type LoginDto =
        { Username: string
          Password: string }

    type TokenResponse =
        { Token: string }
