namespace Amintiri.Api

open Npgsql.FSharp
open Microsoft.Extensions.Configuration

module Database =
    type Config() =
        member val Database = "" with get, set
        member val Password = "" with get, set
        member val Username = "" with get, set
        member val Server = "" with get, set
        member val Port = 0 with get, set

    let defaultConnection (config: Config) =
        Sql.host config.Server
        |> Sql.port config.Port
        |> Sql.username config.Username
        |> Sql.password config.Password
        |> Sql.database config.Database
        |> Sql.sslMode SslMode.Prefer
        |> Sql.config "Pooling=true" // optional Config for connection string
