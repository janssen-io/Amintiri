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
open Giraffe.GiraffeViewEngine

// ---------------------------------
// Views
// ---------------------------------

module Views =

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
        (dict >> json) [ ("id",photoId) ]

    let browsePhotos = text "browsePhotos"

    let appIndex = (htmlView <| Views.layout ([span [] [encodedText "Hello, Giraffe!"]]))

    let webApp =
            GET >=> 
                choose [
                    route "/" >=> appIndex
                ]

    let webApi =
        choose [
            GET >=>
                choose [
                    route "/" >=> indexHandler
                    route "/photos/" >=> browsePhotos
                    routef "/photos/%s" photoHandler
                ]
            POST >=>
                choose [
                    route "/photos/" >=> fileUploadHandler
                ]
        ]

    let routes =
        choose [
            subRoute "/api" webApi
            subRoute "/app" webApp
            setStatusCode 404 >=> text "Not Found"
        ]

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

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
            .UseGiraffe(routes)

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

    let runMigrations (host:IWebHost) =
        using
            (host.Services.CreateScope ())
            (fun scope ->
                let config = scope.ServiceProvider.GetService<IConfiguration> ()
                let dbConfig = Database.defaultConnection config |> Sql.formatConnectionString
                Migrations.execute dbConfig
            )

    [<EntryPoint>]
    let main _ =
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot     = Path.Combine(contentRoot, "WebRoot")
        let host = WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(contentRoot)
                    .UseIISIntegration()
                    .UseWebRoot(webRoot)
                    .ConfigureAppConfiguration(configureAppConfig)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    .Build()

        runMigrations host
        host.Run()
        0
