namespace Amintiri.Photos

module Application =
    open Microsoft.AspNetCore.Http
    open Amintiri.Common
    open Npgsql.FSharp
    open System
    open Domain

    let private insert dbConfig (photo: Photo) =
        if photo.Id.IsSome then
            Error PhotoAlreadyExists
        else
            Sql.formatConnectionString dbConfig
            |> Sql.connect
            |> Sql.query "INSERT INTO photos (id, path, name) VALUES (@id, @path, @name)"
            |> Sql.parameters
                [ "id", SqlValue.Uuid <| Guid.NewGuid()
                  "path", (Path.unwrap >> SqlValue.String) photo.Path
                  "name", SqlValue.String photo.Name ]
            |> Sql.executeNonQuery
            |> Result.mapError UploadError.TechnicalError
            |> Result.map (fun _ -> ())

    let private list dbConfig () =
        Sql.formatConnectionString dbConfig
        |> Sql.connect
        |> Sql.query "SELECT * FROM photos"
        |> Sql.execute (fun row ->
            { Id = row.uuid "id" |> Some
              Path = (row.text "path" |> Path.unsafeCreate)
              Name = row.text "name" })
        |> Result.mapError QueryError.TechnicalError

    let private generateFilename basePath originalFilename =
        let randomName = Guid.NewGuid().ToString("N")
        let extension = IO.Path.GetExtension originalFilename
        let newFilename = sprintf "%s%s" randomName extension
        IO.Path.Combine(basePath, newFilename)

    let private copyFile (file: IFormFile) path =
        use filestream = System.IO.File.Create(Path.unwrap path)
        file.CopyToAsync filestream |> ignore

    let private upload (basePath: Path) (file: IFormFile): Result<Path, UploadError> =
        if file.Length = (int64) 0 then
            Error InvalidPhoto
        else
            (Path.unwrap basePath)
            |> (fun bp -> generateFilename bp file.FileName)
            |> Path.create
            |> function
            | None -> Error InvalidName
            | Some p -> Ok p
            |> Result.map (tee (copyFile file))

    let uploadPhoto basePath dbConfig = PhotoUpload.add (insert dbConfig) (upload (Path.unsafeCreate basePath))

    let browsePhotos dbConfig = PhotoBrowse.list (list dbConfig)
