namespace Amintiri.Photos

module PhotoBrowse =
    open Domain

    let list dbConfig =
        Database.list dbConfig |> Result.map (List.map (fun p ->
                                                  {| Id = p.Id
                                                     Name = p.Name
                                                     Path = Path.unwrap p.Path |}))
