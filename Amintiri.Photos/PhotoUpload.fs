namespace Amintiri.Photos

module PhotoUpload =

    open Microsoft.AspNetCore.Http
    open System.IO

    let private addToDatabase dbConfig name (path: Domain.Path) =
        match Database.insert dbConfig path name with
        | Ok x -> Ok x
        | Error exn -> Error exn.Message

    let private upload (file: IFormFile) =
        if file.Length = (int64) 0 then
            Error "File is empty"
        else
            let path = "/data/" + (Path.GetFileName(Path.GetTempFileName()))
            using (System.IO.File.Create path) (fun f -> file.CopyToAsync f) |> ignore
            Domain.Path.create path |> Ok

    let add dbConfig (file: IFormFile) =
        let insert = addToDatabase dbConfig

        let path =
            match upload file with
            | Ok(Some path) -> Ok path
            | Ok None -> failwith "Unexpected failure"
            | Error p -> Error p

        Result.bind (insert file.FileName) path
