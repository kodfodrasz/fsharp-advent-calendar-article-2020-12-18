module HelloFalco.Program

open Falco
open Falco.Routing
open Falco.HostBuilder
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

// =============
// The endpoints
// =============
let helloHandler: HttpHandler =
  let getMessage (route: RouteCollectionReader) =
    route.GetString "name" "stranger"
    |> sprintf "Hello %s!"

  Request.mapRoute getMessage Response.ofPlainText

let endpointList =
  [ get "/" (Response.ofPlainText "Hello world")
    get "/hello/{name?}" helloHandler ]

// ===========================
// Common initializaition code
// ===========================

// ------------
// Register services
// ------------
let configureServices (services: IServiceCollection) = services.AddFalco() |> ignore

// ------------
// Activate middleware
// ------------
let configureApp (endpoints: HttpEndpoint list) (ctx: WebHostBuilderContext) (app: IApplicationBuilder) =
  let devMode =
    StringUtils.strEquals ctx.HostingEnvironment.EnvironmentName "Development"

  app.UseWhen(devMode, (fun app -> app.UseDeveloperExceptionPage()))
     .UseWhen(not (devMode),
              (fun app ->
                app.UseFalcoExceptionHandler
                  (Response.withStatusCode 500
                   >> Response.ofPlainText "Server error"))).UseHttpsRedirection().UseFalco(endpoints)
  |> ignore

// =======================
// AWS Lambda Startup code
// =======================

// Lambda entry point
type LambdaEntryPoint() =

  // The base class must be set to match the AWS service invoking the Lambda function. If not Amazon.Lambda.AspNetCoreServer
  // will fail to convert the incoming request correctly into a valid ASP.NET Core request.
  //
  // API Gateway REST API                         -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
  // API Gateway HTTP API payload version 1.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
  // API Gateway HTTP API payload version 2.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
  // Application Load Balancer                    -> Amazon.Lambda.AspNetCoreServer.ApplicationLoadBalancerFunction
  //
  // Note: When using the AWS::Serverless::Function resource with an event type of "HttpApi" then payload version 2.0
  // will be the default and you must make Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction the base class.
  inherit Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction()

  override this.Init(builder: IWebHostBuilder) =
    builder
      .ConfigureServices(configureServices)
      .Configure(configureApp endpointList)
    |> ignore


// -----------
// Configure Web host
// -----------
let configureWebHost (endpoints: HttpEndpoint list) (webHost: IWebHostBuilder) =
  webHost
    .ConfigureServices(configureServices)
    .Configure(configureApp endpoints)

// ==========================
// Local Kestrel startup code
// ==========================

// Local execution entry point
[<EntryPoint>]
let main args =
  webHost args {
    configure configureWebHost
    endpoints endpointList
  }
  0
