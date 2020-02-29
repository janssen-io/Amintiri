namespace Amintiri.Api

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2
open Microsoft.Extensions.Configuration
open Npgsql.FSharp
open Amintiri.Domain

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "Imagini" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "Imagini" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------
module App = 

    let fileUploadHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let settings = ctx.GetService<IConfiguration>()
            let dbConfig = Database.defaultConnection settings
            let upload = PhotoUpload.add (dbConfig |> Sql.formatConnectionString)

            task {
                return!
                    (match ctx.Request.HasFormContentType with
                    | false -> RequestErrors.BAD_REQUEST "Bad request"
                    | true  ->
                        ctx.Request.Form.Files
                        |> Seq.map upload
                        |> json) next ctx
            }

    let indexHandler : HttpHandler =
        let unwrapPhoto (photo:Photo) =
            {| Id = photo.Id; Path = Path.unwrap photo.Path; Name = photo.Name |}

        fun (next: HttpFunc) (ctx: HttpContext) ->
            let settings = ctx.GetService<IConfiguration>()
            let dbConfig = Database.defaultConnection settings |> Sql.formatConnectionString
            ((Result.map (List.map unwrapPhoto) >> json) (PhotoBrowse.list dbConfig)) next ctx
    
    let photoHandler (photoId:string) =
        // TODO: check if user (currently not existing) can see this picture
        // Grab path from database and return it to the user with the appropriate content type
        json photoId

    let webApp =
        choose [
            GET >=>
                choose [
                    //route "/" >=> indexHandler "world"
                    route "/" >=> indexHandler
                    routef "/photos/%s" photoHandler
                ]
            POST >=>
                choose [
                    route "/photos/" >=> fileUploadHandler
                ]
            setStatusCode 404 >=> text "Not Found" ]

    // ---------------------------------
    // Error handler
    // ---------------------------------

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    // ---------------------------------
    // Config and Main
    // ---------------------------------

    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8080")
               .AllowAnyMethod()
               .AllowAnyHeader()
               |> ignore

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.EnvironmentName with
        | "Development"  -> app.UseDeveloperExceptionPage()
        | _ -> app.UseGiraffeErrorHandler errorHandler)
            .UseHttpsRedirection()
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp)

    let configureServices (services : IServiceCollection) =
        services.AddCors()    |> ignore
        services.AddGiraffe() |> ignore

    let configureLogging (builder : ILoggingBuilder) =
        builder.AddFilter(fun l -> l.Equals LogLevel.Error)
               .AddConsole()
               .AddDebug() |> ignore

    let configureAppConfig (ctx : WebHostBuilderContext) (config : IConfigurationBuilder) =
        config
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile(sprintf "appsettings.%s.json" ctx.HostingEnvironment.EnvironmentName, true, true)
            .AddJsonFile("secrets.json", true, true)
            .AddEnvironmentVariables()
        |> ignore

    [<EntryPoint>]
    let main _ =
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot     = Path.Combine(contentRoot, "WebRoot")
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(contentRoot)
            .UseIISIntegration()
            .UseWebRoot(webRoot)
            .ConfigureAppConfiguration(configureAppConfig)
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .ConfigureLogging(configureLogging)
            .Build()
            .Run()
        0
