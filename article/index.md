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
