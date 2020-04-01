namespace Amintiri.Photos

open System
open Microsoft.AspNetCore.Http

module Domain =
    type Path = private | Path of string

    module Path =
        let unwrap (Path p) = p

        let create s =
            if String.IsNullOrEmpty s then None else Some(Path s)

        let unsafeCreate s =
            match create s with
            | Some p -> p
            | None -> failwithf "Invalid path '%s'" s

    type Photo =
        { Id: Guid option
          Name: string
          Path: Path }

    type UploadError =
        | PhotoAlreadyExists
        | InvalidPhoto
        | InvalidName
        | TechnicalError of exn

    type UploadPhoto = IFormFile -> Result<unit, UploadError>

    type QueryError = TechnicalError of exn

    type QueryPhotos = Unit -> Result<Photo list, QueryError>
