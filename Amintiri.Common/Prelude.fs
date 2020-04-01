namespace Amintiri.Common

[<AutoOpen>]
module Prelude =
    let tee f x =
        f x |> ignore
        x

    let flip f a b = f b a

    let curry f a b = f (a, b)

    let uncurry f (a, b) = f a b

    let curry3 f a b c = f (a, b, c)

    let uncurry3 f (a, b, c) = f a b c
