namespace Amintiri

open System

module Domain =
    type Path = private Path of string

    module Path =
        let unwrap (Path p) = p
        let create s =
            if String.IsNullOrEmpty s then
                None
            else
                Some (Path s)

