namespace Amintiri.Api.Modules.Common

open System
open Giraffe
open Microsoft.AspNetCore.Http

module Errors =
    type ErrorDto =
        { Id: Guid
          Title: string
          Message: string }

    let create title message =
        { Id = Guid.NewGuid()
          Title = title
          Message = message }

    let create500: HttpFunc -> HttpContext -> HttpFuncResult =
        create "Technical error" "Something went wrong. Please try again later."
        |> json
        |> ServerErrors.internalError
