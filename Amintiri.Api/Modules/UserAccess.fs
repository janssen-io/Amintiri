namespace Amintiri.Api.Modules

open Giraffe
open System
open Microsoft.IdentityModel.Tokens
open System.Text
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2
open System.Security.Cryptography
open Amintiri.Api
open Npgsql.FSharp
open Amintiri.UserAccess.Domain
open Amintiri.UserAccess

module UserAccess =
    type Config() =
        member val Issuer = "" with get, set
        member val Audience = "" with get, set

    [<CLIMutable>]
    type RegistrationDto =
        { Username: string
          Password: string
          ConfirmPassword: string }

    [<CLIMutable>]
    type LoginDto =
        { Username: string
          Password: string }

    let private generateSecret() =
        using (new RNGCryptoServiceProvider()) (fun crypto ->
            let bytes: byte [] = Array.zeroCreate 32
            crypto.GetBytes(bytes)
            Convert.ToBase64String(bytes))

    let secret = generateSecret()

    let private generateToken (config: Config) email =
        let claims =
            [| Claim(JwtRegisteredClaimNames.Sub, email)
               Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) |]

        let expires = Nullable(DateTime.UtcNow.AddHours(1.0))
        let notBefore = Nullable(DateTime.UtcNow)
        let securityKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        let signingCredentials = SigningCredentials(key = securityKey, algorithm = SecurityAlgorithms.HmacSha256)

        let token =
            JwtSecurityToken
                (issuer = config.Issuer, audience = config.Audience, claims = claims, expires = expires,
                 notBefore = notBefore, signingCredentials = signingCredentials)

        let tokenResult = {| Token = JwtSecurityTokenHandler().WriteToken(token) |}

        tokenResult

    let private mapLogin login: Domain.Credentials =
        { Username = Username login.Username
          Password = TextPassword login.Password }

    let handlePostToken uaConfig dbConfig =
        let cnx = Database.defaultConnection dbConfig |> Sql.formatConnectionString
        tryBindForm<LoginDto> json None
            (mapLogin
             >> Authentication.validatePassword cnx
             >> Option.map (fun (Username user) -> generateToken uaConfig user)
             >> json)

    let private registerUser connectionString registration =
        Registration.register connectionString registration
        |> function
        | Ok() -> Ok()
        | Error(IncorrectPasswordConfirmation) -> Error "Incorrect password confirm"
        | Error(DuplicateUsername _) -> Error "User already exists"
        | Error(TechnicalError exn) -> Error exn.Message

    let private mapRegistration (registration: RegistrationDto) =
        { Username = Username registration.Username
          Password = TextPassword registration.Password
          PasswordConfirmation = TextPassword registration.ConfirmPassword }

    let register (config: Database.Config) =
        let dbConfig = Database.defaultConnection config |> Sql.formatConnectionString
        tryBindForm<RegistrationDto> json None
            (mapRegistration
             >> registerUser dbConfig
             >> json)
