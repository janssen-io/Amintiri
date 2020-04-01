namespace Amintiri.Photos

module PhotoUpload =

    open Microsoft.AspNetCore.Http
    open Domain
    open System

    let private createNewPhoto (path, name) =
        { Id = None
          Path = path
          Name = name }

    let internal add
        (addToDatabase: Photo -> Result<unit, UploadError>)
        (uploadFile: IFormFile -> Result<Path, UploadError>)
        : UploadPhoto
        =
        fun file ->
            uploadFile file
            |> Result.map (fun path -> createNewPhoto (path, IO.Path.GetFileNameWithoutExtension(file.FileName)))
            |> Result.bind addToDatabase
