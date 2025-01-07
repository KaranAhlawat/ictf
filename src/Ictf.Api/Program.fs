module Ictf.Api.Program

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

open Ictf.Api.Http
open Ictf.Api.Repos.ConnFactory
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Protocols.OpenIdConnect

open Giraffe

open Ictf.Api.Domain.Types
open Ictf.Api.Repos.UserRepo

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        subRoute "/user" UserRoutes.routes
        setStatusCode 404 >=> text "Not Found"
    ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> setStatusCode 500
    >=> text "Something went wrong while executing the request."

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder: CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001",
            "http://localhost:5173",
            "https://localhost:5173"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
    |> ignore

let configureAuth (opts: AuthenticationOptions) =
    opts.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
    opts.DefaultChallengeScheme <- OpenIdConnectDefaults.AuthenticationScheme

let saveUserInfoFromOidc (ctx: UserInformationReceivedContext) =
    task {
        let userR = ctx.HttpContext.GetService<IUserRepo>()
        let jsonSerializer = ctx.HttpContext.GetService<Json.ISerializer>()
        let logger = ctx.HttpContext.GetLogger("saveUserInfoFromOidc")

        let oidcUser =
            jsonSerializer.Deserialize<OidcUser>(ctx.User.RootElement.GetRawText())

        let provider =
            if ctx.Options.Authority.Contains("google") then
                Provider.Google
            else
                Provider.Github

        let! existingUser = userR.GetUserByProvider oidcUser.sub provider

        match existingUser with
        | Some user ->
            logger.LogInformation(
                "User for {Provider} with Provider ID {Id} exists already, skipping save",
                user.Provider,
                user.ProviderId
            )

            return ()
        | None ->
            logger.LogInformation(
                "Saving OIDC ({Provider}) user to database with Provider ID {Id}",
                string provider,
                oidcUser.sub
            )

            userR.InsertUser {
                Id = 0
                ProviderId = oidcUser.sub
                Username = oidcUser.name
                UserEmail = Some oidcUser.email
                Provider = provider
            }
            |> ignore

            ()
    }

let configureOidc (settings: IConfiguration) (opts: OpenIdConnectOptions) =
    let oidcConf = settings.GetSection "OidcSettings"

    opts.Authority <- oidcConf["Authority"]
    opts.ClientId <- oidcConf["ClientId"]
    opts.ClientSecret <- oidcConf["ClientSecret"]
    opts.SignInScheme <- CookieAuthenticationDefaults.AuthenticationScheme
    opts.ResponseType <- OpenIdConnectResponseType.Code
    opts.SaveTokens <- false
    opts.GetClaimsFromUserInfoEndpoint <- true
    opts.MapInboundClaims <- false
    opts.Scope.Clear()
    opts.Scope.Add("openid")
    opts.Scope.Add("profile")
    opts.Scope.Add("email")
    opts.Events.OnUserInformationReceived <- (fun ctx -> saveUserInfoFromOidc ctx)

let configureAppConfig (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config
        .AddJsonFile("appsettings.json", false, true)
        .AddJsonFile(sprintf "appsettings.%s.json" ctx.HostingEnvironment.EnvironmentName, true)
        .AddEnvironmentVariables()
    |> ignore

let configureLogging (builder: ILoggingBuilder) =
    builder.AddConsole().AddDebug() |> ignore

let configureApp (app: IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()

    (match env.IsDevelopment() with
     | true -> app.UseDeveloperExceptionPage()
     | false -> app.UseGiraffeErrorHandler(errorHandler).UseHttpsRedirection())
        .UseHttpLogging()
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseAuthentication()
        .UseAuthorization()
        .UseGiraffe(webApp)

let configureServices (services: IServiceCollection) =
    let provider = services.BuildServiceProvider()
    let settings = provider.GetService<IConfiguration>()

    // General
    services.AddCors().AddLogging().AddHttpLogging() |> ignore

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    JsonFSharpOptions.FSharpLuLike().AddToJsonSerializerOptions(jsonOptions)

    services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions))
    |> ignore

    services.AddSingleton<NpgsqlConnFactory>(fun _ -> NpgsqlConnFactory(settings.GetConnectionString("Postgres")))
    |> ignore

    services.AddSingleton<IUserRepo, UserRepo>() |> ignore

    // Auth
    services
        .AddAuthentication(configureAuth)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, configureOidc settings)
    |> ignore

    services.AddAuthorization().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "WebRoot")

    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseContentRoot(contentRoot)
                .UseWebRoot(webRoot)
                .ConfigureAppConfiguration(configureAppConfig)
                .Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices)
                .ConfigureLogging(configureLogging)
            |> ignore)
        .Build()
        .Run()

    0
