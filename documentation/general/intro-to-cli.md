Introduction to .NET Core CLI
=============================

The .NET Core CLI is a simple, extensible and standalone set of tools for building, managing and otherwise operating on .NET projects. It will or already includes commands such as compilation, NuGet package management and launching a debugger session. It is intended to be fully featured, enabling extensive library and app development functionality appropriate at the command-line. It should provide everything you'd need to develop an app in an SSH session! It is also intended to be a fundamental building block for building finished experiences in tools such as Visual Studio.

Goals:

- Language agnostic - embrace "common language runtime".
- Target agnostic - multi-targets.
- Simple extensibility and layering - "you had one job!"
- Cross-platform - support and personality.
- Semantic user interface over [MSBuild](https://github.com/Microsoft/msbuild).

Experience 
==========

The [.NET Core command-line tools](https://github.com/dotnet/cli) present the "dotnet" tool as the entry-point tool. It provides higher-level commands, often using multiple tools together to complete a task. It's a convenience wrapper over the other tools, which can also be used directly. "dotnet" isn't magical at all, but a very simple aggregator of other tools.

You can get a sense of using the tools from the examples below.

**dotnet restore**

`dotnet restore` restores dependent package from a given NuGet feed (e.g. NuGet.org) for the project in scope.

**dotnet run**

`dotnet run` compiles and runs your app with one step.

**dotnet build**

`dotnet build` compiles your app or library as an IL binary.

Design
======

There are a couple of moving pieces that you make up the general design of the .NET Core CLI:

* The `dotnet` driver
* Specific commands that are part of the package

The `dotnet` driver is very simple and its primary role is to run commands and give users basic information about usage. 

The way the `dotnet` driver finds the command it is instructed to run using `dotnet {command}` is via a convention; any executable that is placed in the PATH and is named `dotnet-{command}` will be available to the driver. For example, when you install the CLI toolchain there will be an executable called `dotnet-build` in your PATH; when you run `dotnet build`, the driver will run the `dotnet-build` executable. All of the arguments following the command are passed to the command being invoked. So, in the invocation of `dotnet build --native`, the `--native` switch will be passed to `dotnet-build` executable that will do some action based on it (in this case, produce a single native binary).

Adding a new command to the .NET Core CLI 
=========================================

If you want to contribute to the actual .NET Core CLI by adding a new command that you think would be useful, refer to the [developer guide](../project-docs/developer-guide.md) in this directory. It contains all of the guidance on both the process as well as the infrastructure that you need to adhere to when adding a new command to the CLI toolchain. 

After you familiarize yourself with the process of working with the source code in the repo, consult the [CLI UX guidelines](cli-ux-guidelines.md) to get to know the user experience tenants the CLI has. 

Adding a new command locally
============================ 
If you wish to extend the CLI, you can read more about supported extensibility models in the [official extensibility document](https://docs.microsoft.com/en-us/dotnet/articles/core/tools/extensibility)/. 

Guidance on how to write a command 
==================================
How you write a given command depends largely on whether you are trying to add it to the CLI project or want to add the command locally, that is on your machine or server. 

For the former case, the [developer guide](../project-docs/developer-guide.md) has all of the details that you will need to get going. 

If you are adding a command on your own machine(s), then there is really no special model to keep in mind. However, since your users will be using the local commands through the `dotnet` driver, we strongly suggest to keep to the principles outlined in the [CLI UX guidelines](cli-ux-guidelines.md) to have an unified user experience for your users. 
