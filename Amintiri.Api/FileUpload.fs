namespace Amintiri.Api

module FileUpload =

    open Microsoft.AspNetCore.Http
    open System.IO

    let addToDatabase name (path:Amintiri.Domain.Path) =
        Database.Photos.insert Database.defaultConnection path name
        |> ignore

    let upload (file : IFormFile) =
        if file.Length = (int64)0 then
            None
        else
            let path = Path.GetTempFileName ()
            using (System.IO.File.Create path) (fun f -> file.CopyToAsync f)
            |> ignore
            (Some << Amintiri.Domain.Path) path

    let add (file : IFormFile) =
        let name = "First upload"
        let path = upload file
        Option.map (addToDatabase name) path

