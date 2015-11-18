Intro to .NET Core CLI
======================

The .NET Core CLI is a simple, extensible and standalone set of tools for building, managing and otherwise operating on .NET projects. It will or already includes commands such as compilation, NuGet package management and launching a debugger session. It is intended to be fully featured, enabling extensive library and app development functionality appropriate at the command-line. It should provide everything you'd need to develop an app in an SSH session! It is also intended to be a fundamental building block for building finished experiences in tools such as Visual Studio.

Goals:

- Language agnostic - embrace "common language runtime".
- Target agnostic - multi-targets.
- Runtime agnostic.
- Simple extensibility and layering - "you had one job!"
- Cross-platform - support and personality.
- Outside-in philosphy - higher-level tools drive the CLI.

Historical Context - DNX
========================

We've been using [DNX](http://blogs.msdn.com/b/dotnet/archive/2015/04/29/net-announcements-at-build-2015.aspx#dnx) for all .NET Core scenarios for nearly two years. It provides a lot of great experiences, but doesn't have great "pay for play" characteristics. DNX is a big leap from  building the [CoreCLR](https://github.com/dotnet/coreclr) and [CoreFX](https://github.com/dotnet/corefx) repos and wanting to build an app with a simple environment. In fact, one of the open source contributors to CoreCLR said: "I can build CoreCLR, but I don't know how to build 'Hello World'." We cannot have that!

.NET Core includes three new components: a set of standalone command-line (CLI) tools, a shared framework and a set of runtime services. These components will replace DNX and are essentially DNX split in three parts. 

The DNX services will be offered as a hosting option available to apps. You can opt to use a host that offers one or more of these services, like file change watching or NuGet package servicing. You can also opt to use a shared framework, to ease deployment of dependencies and for performance reasons. Some of this is still being designed and isn't yet implemented.

ASP.NET 5 will transition to the new tools for RC2. This is already in progress. There will be a smooth transition from DNX to these new .NET Core components.

Experience 
==========

The [CLI tools](https://github.com/dotnet/cli) present the "dotnet" tool as the entry-point tool. It provides higher-level commands, often using multiple tools together to complete a task. It's a convenience wrapper over the other tools, which can also be used directly. "dotnet" isn't magical at all, but a very simple aggregator of other tools.

You can get a sense of using the tools from the examples below.

**dotnet restore**

`dotnet restore` restores dependent package from a given NuGet feed (e.g. NuGet.org) for the project in scope.

**dotnet run**

`dotnet run` compiles and runs your app with one step. Same as `dnx run`.

**dotnet compile**

`dotnet compile --native` native compiles your app into a single executable file.

`dotnet compile` compiles your app or library as an IL binary. In the case of an app, `compile` generates runable assets by copying an executable host to make the IL binary runable. The host relies on a shared framework for dependencies, including a runtime.

Design
======

More content here.




