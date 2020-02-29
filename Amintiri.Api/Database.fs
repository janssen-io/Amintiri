namespace Amintiri.Api

open Amintiri.Domain
open Npgsql.FSharp
open Microsoft.Extensions.Configuration

module Database =

    let defaultConnection (config : IConfiguration) =
        Sql.host config.["postgres_server"]
        |> Sql.port (int config.["postgres_port"])
        |> Sql.username config.["postgres_user"]
        |> Sql.password config.["postgres_password"]
        |> Sql.database config.["postgres_database"]
        |> Sql.sslMode SslMode.Prefer
        |> Sql.config "Pooling=true" // optional Config for connection string

    module Photos =
                
        let insert connectionString (path:Path) (name:string) =
            Sql.connect connectionString
            |> Sql.query "INSERT INTO photos (path, name) VALUES (@path, @name)"
            |> Sql.parameters [ 
                "path", (Path.unwrap >> SqlValue.String) path
                "name", SqlValue.String name
            ]  
            |> Sql.executeNonQuery

        let list connectionString =
            Sql.connect connectionString
            |> Sql.query "SELECT * FROM photos"
            |> Sql.execute (fun row ->
                {
                    Id = row.int "id"
                    Path = (row.text "path" |> Path.unsafeCreate)
                    Name = row.text "name"
                }
            )

