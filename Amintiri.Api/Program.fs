namespace Amintiri.Api

open Amintiri.Api.Modules
open Giraffe
open Giraffe.GiraffeViewEngine
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.IdentityModel.Tokens
open Npgsql.FSharp
open System
open System.IO
open System.Text

// ---------------------------------
// Views
// ---------------------------------

module Views =

    let layout (content: XmlNode list) =
        html []
            [ head []
                  [ title [] [ encodedText "Imagini" ]
                    link
                        [ _rel "stylesheet"
                          _type "text/css"
                          _href "/main.css" ] ]
              body [] content ]

module App =

    type ConfigurationSection =
        | Authorization
        | Postgres

    let getSection<'t when 't: (new: unit -> 't)> (section: ConfigurationSection) (appConfig: IConfiguration) =
        let mutable config = new 't()
        appConfig.GetSection(section.ToString()).Bind config
        config

    let getDbConfig config = getSection<Database.Config> Postgres config
    let getUserAccessConfig config = getSection<UserAccess.Config> Authorization config

    let authorize = requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

    let appIndex = (htmlView <| Views.layout ([ span [] [ encodedText "Hello, Giraffe!" ] ]))
    let apiIndex = (dict >> json) [ "version", 0 ]

    let authEndpoints next (ctx: HttpContext) =
        let uaConfig = getUserAccessConfig (ctx.GetService())
        let dbConfig = getDbConfig (ctx.GetService())
        choose
            [ POST >=> route "/token" >=> UserAccess.handlePostToken uaConfig dbConfig
              POST >=> route "/register" >=> UserAccess.register dbConfig ] next ctx

    let appEndpoints = GET >=> choose [ route "/" >=> appIndex ]

    let apiEndpoints =
        choose
            [ GET >=> choose
                          [ route "/" >=> apiIndex
                            route "/photos/" >=> (fun f ctx ->
                            let dbConfig = getDbConfig (ctx.GetService())
                            Photos.browsePhotos dbConfig f ctx)
                            routef "/photos/%s" Photos.photoHandler ]
              POST >=> choose
                           [ route "/photos/" >=> (fun f ctx ->
                             let dbConfig = getDbConfig (ctx.GetService())
                             Photos.fileUploadHandler dbConfig f ctx) ] ]

    let routes =
        choose
            [ subRoute "/api" authorize >=> apiEndpoints
              subRoute "/app" appEndpoints
              subRoute "/auth" authEndpoints
              setStatusCode 404 >=> text "Not Found" ]

    let errorHandler (ex: Exception) (logger: ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    let configureCors (builder: CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyHeader() |> ignore

    let configureApp (app: IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.EnvironmentName with
         | "Development" -> app.UseDeveloperExceptionPage()
         | _ -> app.UseGiraffeErrorHandler errorHandler).UseAuthentication().UseHttpsRedirection()
            .UseCors(configureCors).UseStaticFiles().UseGiraffe(routes)

    let configureServices (services: IServiceCollection) =
        services.AddCors() |> ignore
        services.AddGiraffe() |> ignore

        let svc = services.BuildServiceProvider()
        let uaConfig = getUserAccessConfig (svc.GetService())

        let authOptions (o: AuthenticationOptions) =
            o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
            o.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme

        let jwtBearerOptions (cfg: JwtBearerOptions) =
            cfg.SaveToken <- true
            cfg.IncludeErrorDetails <- true
            cfg.Audience <- uaConfig.Audience
            cfg.TokenValidationParameters <-
                new TokenValidationParameters(ValidIssuer = uaConfig.Issuer,
                                              IssuerSigningKey =
                                                  SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserAccess.secret)))

        services.AddAuthentication(authOptions).AddJwtBearer(Action<JwtBearerOptions> jwtBearerOptions) |> ignore

    let configureLogging (builder: ILoggingBuilder) =
        builder.AddFilter(fun l -> l.Equals LogLevel.Information).AddConsole().AddDebug() |> ignore

    let configureAppConfig (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
        config.AddJsonFile("appsettings.json", false, true)
              .AddJsonFile(sprintf "appsettings.%s.json" ctx.HostingEnvironment.EnvironmentName, true, true)
              .AddJsonFile("secrets.json", true, true).AddEnvironmentVariables() |> ignore

    let runMigrations (host: IWebHost) =
        let config = getDbConfig (host.Services.GetService())
        let dbConfig = Database.defaultConnection config |> Sql.formatConnectionString
        Migrations.execute dbConfig

    [<EntryPoint>]
    let main _ =
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot = Path.Combine(contentRoot, "WebRoot")
        let host =
            WebHostBuilder().UseKestrel().UseContentRoot(contentRoot).UseIISIntegration().UseWebRoot(webRoot)
                .ConfigureAppConfiguration(configureAppConfig).Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices).ConfigureLogging(configureLogging).Build()

        runMigrations host
        host.Run()
        0
