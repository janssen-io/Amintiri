namespace Amintiri.Api.Modules.Photos

open System
open Microsoft.AspNetCore.Http

module Types =

    type Config() =
        member val BasePath = "" with get, set

    type PhotoDto =
        { Id: Guid
          Url: Uri
          Name: string }

    [<CLIMutable>]
    type UploadRequest =
        { File: IFormFile
          Name: string }
