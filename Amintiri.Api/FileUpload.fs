namespace Amintiri.Api

module FileUpload =

    open Microsoft.AspNetCore.Http
    open System.IO

    let private addToDatabase dbConfig name (path:Amintiri.Domain.Path) =
        match Database.Photos.insert dbConfig path name with
        | Ok x -> Ok x
        | Error exn -> Error exn.Message
        
    let private upload (file : IFormFile) =
        if file.Length = (int64)0 then
            Error "File is empty"
        else
            let path = Path.GetTempFileName ()
            using (System.IO.File.Create path) (fun f -> file.CopyToAsync f)
            |> ignore
            (Ok << Amintiri.Domain.Path.create) path

    let add dbConfig (file : IFormFile) =
        let insert = addToDatabase dbConfig

        let path = 
            match upload file with
            | Ok (Some path) -> Ok path
            | Ok None -> failwith "Unexpected failure"
            | Error p -> Error p

        Result.bind (insert file.FileName) path

