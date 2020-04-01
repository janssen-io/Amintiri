namespace Amintiri.Api.Modules.Photos

open Amintiri.Api
open Amintiri.Api.Modules.Common
open Amintiri.Photos
open Amintiri.Photos.Domain
open FSharp.Control.Tasks.V2
open Giraffe
open Microsoft.AspNetCore.Http
open System
open Types

module Endpoints =

    type UploadRequestError =
        | NoFiles
        | DomainError of UploadError

    let private fileUploadHandler (photoConfig: Config) dbConfig =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let sqlConfig = Database.defaultConnection dbConfig
            let upload = Application.uploadPhoto photoConfig.BasePath sqlConfig

            let validateUploadRequest (request: HttpRequest) =
                if not request.HasFormContentType then
                    Error NoFiles
                else
                    match Seq.tryHead request.Form.Files with
                    | Some file -> Ok file
                    | None -> Error NoFiles

            let mapSuccess _ = Successful.NO_CONTENT

            let mapError error =
                match error with
                | NoFiles ->
                    Errors.create "No data" "The request contains no picture."
                    |> json
                    |> RequestErrors.badRequest
                | DomainError error' ->
                    match error' with
                    | PhotoAlreadyExists ->
                        Errors.create "Duplicate entry" "This picture has already been uploaded."
                        |> json
                        |> RequestErrors.badRequest
                    | InvalidPhoto ->
                        Errors.create "Invalid file" "This file does not contain a picture."
                        |> json
                        |> RequestErrors.badRequest
                    | InvalidName ->
                        Errors.create "Invalid name" "This picture has an invalid name."
                        |> json
                        |> RequestErrors.badRequest
                    | UploadError.TechnicalError exn ->
                        // TODO: log exception
                        Errors.create "Technical error" "Something went wrong. Please try again later."
                        |> json
                        |> ServerErrors.internalError

            task {
                return! validateUploadRequest ctx.Request
                        |> Result.bind (upload >> Result.mapError DomainError)
                        |> Result.fromEither mapSuccess mapError
                        |> (fun dto -> dto next ctx)
            }

    let private mapToDto (photo: Photo) =
        { Id = photo.Id.Value
          Name = photo.Name
          Url = new Uri(sprintf "/photos/%s" (photo.Id.Value.ToString("N")), UriKind.Relative) }

    let private mapBrowseResult (result: Result<Photo list, QueryError>) =
        match result with
        | Ok photos ->
            List.map mapToDto photos
            |> json
            |> Successful.ok
        | Error error ->
            match error with
            // TODO: log exception
            | TechnicalError exn -> Errors.create500

    let private browsePhotos config =
        let dbConfig = Database.defaultConnection config
        Application.browsePhotos dbConfig |> mapBrowseResult

    let private photoHandler (photoId: string) = (dict >> json) [ ("id", photoId) ]

    let routes photoConfig dbConfig =
        choose
            [ GET >=> choose
                          [ route "/" >=> browsePhotos dbConfig
                            routef "/%s" photoHandler ]
              POST >=> choose [ route "/" >=> fileUploadHandler photoConfig dbConfig ] ]
