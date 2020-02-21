namespace Imagini.Api

module FileUpload =

    open Microsoft.AspNetCore.Http
    open System.IO

    let addToDatabase name (path:Imagini.Domain.Path) =
        let cnx = Database.createConnection ()
        cnx.Open ()
        Database.Photos.insert cnx path name
        |> ignore
        cnx.Close ()

    let upload (file : IFormFile) =
        if file.Length = (int64)0 then
            None
        else
            let path = Path.GetTempFileName ()
            using (System.IO.File.Create path) (fun f -> file.CopyToAsync f)
            |> ignore
            (Some << Imagini.Domain.Path) path

    let add (file : IFormFile) =
        let name = "First upload"
        let path = upload file
        Option.map (addToDatabase name) path

