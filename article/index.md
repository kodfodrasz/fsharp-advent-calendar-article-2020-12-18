title: Running ASP.Net web application with Falco on AWS Lambda
tags:
 - dotnet
 - dotnet core
 - .Net
 - .Net Core
 - F#
 - fsharp
 - aws
 - lambda
 - falco
 - asp
 - asp.net
---

We have been using AWS Lambda with F# for a while and have some experience with it. Until now, we opted to use the plain AWS Lambda .Net Runtime provided by Amazon because we value simplicity and code being transparent so we can reason about its operation. This is also why we opted for using F#, as it provides a sweet spot in a simple to reason about functional code and rich features provided by the .Net Framework, all with good performance for our use case.

Still, we have met some pain points with developing for and running on AWS Lambda:

One is that the edit-compile-run loop takes a very long time as it involves deployment to Lambda. Testing and interactive debugging are really slow this way. To overcome this, one needs an emulator environment for the Lambda Runtime. Developing one is additional maintenance overhead, so the lookout for a suitable out-of-the-box solution became a background task in my mind.

While reading comments about the announcement of F# 5.0, I found out about the Falco framework, which is a functional-first Asp.Net Core framework. I wondered whether opting for and Asp.Net hosting in Lambda could provide a setup to allow simple local/CI execution while also providing a mostly similar hosting environment in AWS Lambda... so I started to build an experimental setup to evaluate the idea.

The experiment has the main steps:

 1. Getting ASP.Net & Falco running locally with a simple web application
 2. Deploying to AWS Lambda
 3. Using .Net 5.0 in AWS Lambda


# Getting ASP.Net & Falco running locally with a simple web application

## Introducing Falco

[Falco Framework](https://www.falcoframework.com/) is an F# web application framework, based on ASP.Net Core. It is still a work in progress, but its minimalism and non-intrusive API made it really sympathetic for me. I have checked out some other F# web frameworks, but this is the first, which feels cleaner and more straightforward than only using ASP.Net the same object-oriented way I used it from C# in the past.

## Hello World from local Falco

First, let's create a simple sample app to test the hosting setup. Let's stick to the [Falco's Getting Started Guide](https://www.falcoframework.com/#getting-started).

Let's create the official HelloWorld application!

~~~sh
dotnet new -i "Falco.Template::*"
dotnet new falco -o HelloFalco
~~~

This installs the Falco templates, then creates a project in the `HelloFalco` directory based on the `falco` template just installed.

I usually use Visual Studio, so adding a solution for the project is also a good idea (for me).

~~~sh
dotnet new solution -n HelloFalco
dotnet sln add HelloFalco
~~~

Now I can simply open the solution and check the `Program.fs` file. For now, we simply stick to the contents in the template.

Let's start it! If using Visual Studio choose the HelloWordApp from the run configurations to use the Kestrel based hosting instead of IIS Express.

![Where to select that run configuration](part1-run_config.jpg)

Now you should see something like this:

![It runs!](part1-it_runs.jpg)

## Adding a route

Before we move on to the next phase, let's add another – parameterized – route from the Falco examples. This will be used to validate the hosting setup in the next phase. The complete program is still very compact, looks as follows:

~~~fsharp
module HelloFalco.Program

open Falco
open Falco.Routing
open Falco.HostBuilder
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

// ------------
// Register services
// ------------
let configureServices (services : IServiceCollection) =
    services.AddFalco() |> ignore

// ------------
// Activate middleware
// ------------
let configureApp (endpoints : HttpEndpoint list) (ctx : WebHostBuilderContext) (app : IApplicationBuilder) =
    let devMode = StringUtils.strEquals ctx.HostingEnvironment.EnvironmentName "Development"
    app.UseWhen(devMode, fun app ->
            app.UseDeveloperExceptionPage())
       .UseWhen(not(devMode), fun app ->
            app.UseFalcoExceptionHandler(Response.withStatusCode 500 >> Response.ofPlainText "Server error"))
       .UseFalco(endpoints) |> ignore

// -----------
// Configure Web host
// -----------
let configureWebHost (endpoints : HttpEndpoint list) (webHost : IWebHostBuilder) =
    webHost
        .ConfigureServices(configureServices)
        .Configure(configureApp endpoints)

let helloHandler: HttpHandler =
  let getMessage (route: RouteCollectionReader) =
    route.GetString "name" "stranger"
    |> sprintf "Hello %s!"

  Request.mapRoute getMessage Response.ofPlainText

[<EntryPoint>]
let main args =
  webHost args {
    configure configureWebHost

    endpoints [ get "/" (Response.ofPlainText "Hello world")
                get "/hello/{name?}" helloHandler ]
  }
  0
~~~

Its operation can be quickly verified with cURL.

![Image of locally running service responding to queries as expected](part1_additional_routes.jpg)

It runs and responds as expected. 🎉👏

Ok, nothing extraordinary yet. We managed to start a vanilla demo template then extended it with some other demo code. Time for something more interesting.


# Deploying to AWS Lambda

We have managed to reproduce the official Falco tutorial so far. As mentioned earlier, we are actively using AWS Lambda, so now I will show how to deploy a Falco application to Lambda!

For this, I will be using [AWS Serverless Application Model](https://docs.aws.amazon.com/serverless-application-model/), with its .Net tooling. 
I have also used some AWS Lambda function templates as a basis for the configuration files I will present below. All of these can be installed using the dotnet command.

~~~sh
dotnet tool install -g Amazon.Lambda.Tools
dotnet new -i "Amazon.Lambda.Templates::*"
~~~

The template I used is the `serverless.AspNetCoreWebAPI` one. I interested you can check it out with `dotnet new serverless.AspNetCoreWebAPI --language F# --name HelloServerlessAsp`.

## AWS infrastructure prerequisites

Deployment to Lambda using AWS SAM needs a bucket for the storage of the packaged Lambda functions. Assuming your AWS CLI is installed and your credentials are configured, it is a straightforward command to create it. I created the bucket `lambda.kodfodrasz.net` using my default credentials in the region closest to me.

~~~sh
aws s3api create-bucket --bucket lambda.kodfodrasz.net --acl private --create-bucket-configuration LocationConstraint=eu-central-1
~~~

## Configuring a Lambda compatible .Net version

As of writing this article .Net 5.0 has been recently released. However, it is not supported out-of-the-box officially by Amazon, though workarounds exist. 
I have .Net 5.0 installed on my machine, which would result in the app targeting this version, unless configured, which would ultimately result in runtime failures at Lambda invocation time.

To prevent the problems, we must adjust the version settings to target the latest supported runtime version in AWS Lambda: .Net Core 3.1.

First, let's retarget the `HelloFalco.fsproj` project, to contain `<TargetFramework>netcoreapp3.1</TargetFramework>`.

Second, a `global.json` file needs to be added and set up to prevent [automatic version *roll forward*](https://docs.microsoft.com/en-us/dotnet/core/tools/global-json?tabs=netcore3x#rollforward). Add the `global.json` file to the root of the projects (the solution directory).

~~~json
{
  "sdk": {
    "version": "3.1.100",
    "rollForward": "latestMinor"
  }
}
~~~

## Adapt the F# project for use with AWS SAM tools

The Amazon Serverless templates have some special fields set up in their project file. We should add these to ensure Amazon tools handle the project properly.

Also, we will need an additional NuGet package, an adapter between the Lambda runtime and ASP.Net. You could add it using the command `dotnet add package Amazon.Lambda.AspNetCoreServer`, but I simply tuned the `HelloFalco.fsproj` to include these changes mentioned.

However, this is not enough. We need to add the AWS Lambda facette to the Falco project file `HelloFalco.fsproj` as well:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <!-- The next three properties were copied from the AWS Serverless template to ensure tooling compatibility (along with the next comment). -->
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>

    <!-- This property makes the build directory similar to a publish directory and helps the AWS .NET Lambda Mock Test Tool find project dependencies. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Falco" Version="3.0.*" />
    <!-- Contains the adapter between AWS Lambda Runtime and ASP.Net Core -->
    <PackageReference Include="Amazon.Lambda.AspNetCoreServer" Version="5.2.0" />
  </ItemGroup>
</Project>
```
