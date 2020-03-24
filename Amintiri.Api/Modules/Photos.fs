namespace Amintiri.Api.Modules


open Amintiri.Api
open Amintiri.Photos

open FSharp.Control.Tasks.V2
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Npgsql.FSharp

module Photos =

    let fileUploadHandler config =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let dbConfig = Database.defaultConnection config
            let upload = PhotoUpload.add (dbConfig |> Sql.formatConnectionString)

            task {
                return! (match ctx.Request.HasFormContentType with
                         | false -> RequestErrors.BAD_REQUEST "Bad request"
                         | true ->
                             ctx.Request.Form.Files
                             |> Seq.map upload
                             |> json) next ctx
            }

    let browsePhotos config (next: HttpFunc) (ctx: HttpContext) =
        let dbConfig = Database.defaultConnection config |> Sql.formatConnectionString
        (json <| PhotoBrowse.list dbConfig) next ctx

    let photoHandler (photoId: string) = (dict >> json) [ ("id", photoId) ]
