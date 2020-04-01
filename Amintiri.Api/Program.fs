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
        | Photos

    let private getSection<'t when 't: (new: unit -> 't)> (section: ConfigurationSection) (appConfig: IConfiguration) =
        let mutable config = new 't()
        appConfig.GetSection(section.ToString()).Bind config
        config

    let private getDbConfig config = getSection<Database.Config> Postgres config
    let private getUserAccessConfig config = getSection<UserAccess.Types.Config> Authorization config
    let private getPhotoConfig config = getSection<Photos.Types.Config> Photos config

    let private authorize = requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

    let private appIndex = (htmlView <| Views.layout ([ span [] [ encodedText "Hello, Giraffe!" ] ]))

    let private apiIndex = (dict >> json) [ "version", 0 ]

    let private apiEndpoints photoConfig dbConfig =
        authorize >=> choose
                          [ route "/" >=> apiIndex
                            subRoute "/photos" (Photos.Endpoints.routes photoConfig dbConfig) ]

    let private appEndpoints = GET >=> route "/" >=> appIndex

    let private routes (next: HttpFunc) (ctx: HttpContext) =
        let appConfig = ctx.GetService<IConfiguration>()
        let authConfig = getUserAccessConfig appConfig
        let dbConfig = getDbConfig appConfig
        let photoConfig = getPhotoConfig appConfig

        let api = apiEndpoints photoConfig dbConfig

        choose
            [ subRoute "/api" api
              subRoute "/app" appEndpoints
              subRoute "/auth" (UserAccess.Endpoints.routes authConfig dbConfig)
              setStatusCode 404 >=> text "Not Found" ] next ctx

    let private errorHandler (ex: Exception) (logger: ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    let private configureCors (builder: CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyHeader() |> ignore

    let private configureApp (app: IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.EnvironmentName with
         | "Development" -> app.UseDeveloperExceptionPage()
         | _ -> app.UseGiraffeErrorHandler errorHandler).UseAuthentication().UseHttpsRedirection()
            .UseCors(configureCors).UseStaticFiles().UseGiraffe(routes)

    let private configureServices (services: IServiceCollection) =
        services.AddCors() |> ignore
        services.AddGiraffe() |> ignore

        let svc = services.BuildServiceProvider()
        let authConfig = getUserAccessConfig (svc.GetService())

        let authOptions (o: AuthenticationOptions) =
            o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
            o.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme

        let securityKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserAccess.Endpoints.jwtSharedSecret authConfig))

        let jwtBearerOptions (cfg: JwtBearerOptions) =
            cfg.SaveToken <- true
            cfg.IncludeErrorDetails <- true
            cfg.Audience <- authConfig.Audience
            cfg.TokenValidationParameters <-
                new TokenValidationParameters(ValidIssuer = authConfig.Issuer, IssuerSigningKey = securityKey)

        services.AddAuthentication(authOptions).AddJwtBearer(Action<JwtBearerOptions> jwtBearerOptions) |> ignore

    let private configureLogging (builder: ILoggingBuilder) =
        builder.AddFilter(fun l -> l.Equals LogLevel.Information).AddConsole().AddDebug() |> ignore

    let private configureAppConfig (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
        config.AddJsonFile("appsettings.json", false, true)
              .AddJsonFile(sprintf "appsettings.%s.json" ctx.HostingEnvironment.EnvironmentName, true, true)
              .AddJsonFile("secrets.json", true, true).AddEnvironmentVariables() |> ignore

    let private runMigrations (host: IWebHost) =
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
