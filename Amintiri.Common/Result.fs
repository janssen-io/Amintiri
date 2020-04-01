[<AutoOpen>]
module Result

let fromEither onSuccess onError: Result<'a, 'b> -> 'c =
    function
    | Ok success -> onSuccess success
    | Error error -> onError error
