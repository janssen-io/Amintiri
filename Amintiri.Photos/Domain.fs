namespace Amintiri.Photos

open System

module internal Domain =
    type Path = private Path of string

    type Photo =
        { Id: Guid
          Name: string
          Path: Path }

    module Path =
        let unwrap (Path p) = p

        let create s =
            if String.IsNullOrEmpty s then None
            else Some(Path s)

        let unsafeCreate s =
            match create s with
            | Some p -> p
            | None -> failwithf "Invalid path '%s'" s
