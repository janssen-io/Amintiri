namespace Amintiri.Api

open MySql.Data
open MySql.Data.MySqlClient
open Amintiri.Domain

module Database =

    [<Literal>]
    let connectionString = 
        "User=root;Password=dbaroot;Database=amintiri;Server=localhost;Port=3306;"

    let createConnection () = new MySqlConnection(connectionString)

    module Photos =

        let insert (cnx:MySqlConnection) (path:Path) (name:string) =
            let sql = "INSERT INTO photos (path, name) VALUES (@Path, @Name)"
            let cmd = new MySqlCommand (sql, cnx)
            cmd.Parameters.Add (new MySqlParameter ("@Path", Path.unwrap path))
            |> ignore
            cmd.Parameters.Add (new MySqlParameter ("@Name", name))
            |> ignore
            cmd.ExecuteNonQuery ()
                



        

