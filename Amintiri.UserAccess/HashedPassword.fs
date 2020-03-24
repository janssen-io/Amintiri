namespace Amintiri.UserAccess

module internal HashedPassword =

    open System.Security.Cryptography
    open System
    open Microsoft.AspNetCore.Cryptography.KeyDerivation
    open Domain

    let private saltSize = 128 / 8
    let private subkeySize = 256 / 8

    let private fromBytes = Convert.ToBase64String
    let private toBytes = Convert.FromBase64String

    let private generateSalt() =
        use crypto = new RNGCryptoServiceProvider()
        let salt: byte [] = Array.zeroCreate saltSize
        crypto.GetBytes salt

        fromBytes salt |> Salt

    let hashWithSalt (Salt salt) (TextPassword password) =
        { Salt = Salt salt
          Hash =
              Convert.ToBase64String
                  (KeyDerivation.Pbkdf2(password, toBytes salt, KeyDerivationPrf.HMACSHA1, 10_000, subkeySize)) }

    let hash password: HashedPassword = hashWithSalt (generateSalt()) password
