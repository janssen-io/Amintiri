namespace Amintiri.Api

open Amintiri.Domain
open Npgsql.FSharp

module Database =

    let defaultConnection  =
        Sql.host "localhost"
        |> Sql.port 5432
        |> Sql.username "user"
        |> Sql.password "password"
        |> Sql.database "amintiri"
        |> Sql.sslMode SslMode.Require
        |> Sql.config "Pooling=true" // optional Config for connection string
        |> Sql.formatConnectionString

    module Photos =
                
        let insert connectionString (path:Path) (name:string) =
            Sql.connect connectionString
            |> Sql.query "INSERT INTO photos (path, name) VALUES (@path, @name)"
            |> Sql.parameters [ 
                "path", (Path.unwrap >> SqlValue.String) path
                "name", SqlValue.String name
            ]  
            |> Sql.executeNonQuery


        

