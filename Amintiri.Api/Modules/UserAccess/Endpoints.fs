namespace Amintiri.Api.Modules.UserAccess

open Amintiri.Api
open Amintiri.UserAccess
open Amintiri.UserAccess.Domain
open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open Types

module Endpoints =
    let private generateSecret() =
        using (new RNGCryptoServiceProvider()) (fun crypto ->
            let bytes: byte [] = Array.zeroCreate 32
            crypto.GetBytes(bytes)
            Convert.ToBase64String(bytes))

    let jwtSharedSecret (config: Config) =
        if String.IsNullOrEmpty config.AppKey then generateSecret() else config.AppKey

    let private generateToken (config: Config) email =
        let claims =
            [| Claim(JwtRegisteredClaimNames.Sub, email)
               Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) |]

        let expires = Nullable(DateTime.UtcNow.AddHours(1.0))
        let notBefore = Nullable(DateTime.UtcNow)
        let securityKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSharedSecret config))
        let signingCredentials = SigningCredentials(key = securityKey, algorithm = SecurityAlgorithms.HmacSha256)

        let token =
            JwtSecurityToken
                (issuer = config.Issuer, audience = config.Audience, claims = claims, expires = expires,
                 notBefore = notBefore, signingCredentials = signingCredentials)

        JwtSecurityTokenHandler().WriteToken(token)

    let private mapLogin login: Domain.Credentials =
        { Username = Username login.Username
          Password = TextPassword login.Password }

    let private createLoginResponse authConfig (authResult: Result<Username, AuthenticationError>) =
        match authResult with
        | Ok(Username user) -> json { Token = generateToken authConfig user } |> Successful.ok
        | Error error ->
            match error with
            | IncorrectUsername
            | IncorrectPassword ->
                json {| error = "Invalid username or password" |}
                |> RequestErrors.unauthorized JwtBearerDefaults.AuthenticationScheme ""
            // TODO: log exception
            | AuthenticationError.TechnicalError exn -> ServerErrors.internalError (json "Something went wrong...")

    let private mapRegistration (registration: RegistrationDto) =
        { Username = Username registration.Username
          Password = TextPassword registration.Password
          PasswordConfirmation = TextPassword registration.ConfirmPassword }

    let private createRegistrationResponse (result: Result<unit, RegistrationError>) =
        match result with
        | Ok _ -> Successful.NO_CONTENT
        | Error error ->
            match error with
            | IncorrectPasswordConfirmation -> RequestErrors.badRequest (json "Incorrect password confirmation")
            | DuplicateUsername _ -> RequestErrors.badRequest (json "User already exists")
            // TODO: log exception
            | RegistrationError.TechnicalError _ -> ServerErrors.internalError (json "Something went wrong...")

    let handlePostToken authConfig config =
        let dbConfig = Database.defaultConnection config
        tryBindForm<LoginDto> json None
            (mapLogin
             >> Application.authenticateUser dbConfig
             >> createLoginResponse authConfig)

    let register (config: Database.Config) =
        let dbConfig = Database.defaultConnection config
        tryBindForm<RegistrationDto> json None
            (mapRegistration
             >> Application.registerUser dbConfig
             >> createRegistrationResponse)

    let routes authConfig dbConfig =
        choose
            [ POST >=> route "/token" >=> handlePostToken authConfig dbConfig
              POST >=> route "/register" >=> register dbConfig ]
