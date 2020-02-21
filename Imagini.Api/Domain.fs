namespace Imagini

module Domain =
    type Path = Path of string

    module Path =
        let unwrap (Path p) = p

