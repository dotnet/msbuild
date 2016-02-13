Intro to .NET Core CLI
======================

The .NET Core CLI is a simple, extensible and standalone set of tools for building, managing and otherwise operating on .NET projects. It will or already includes commands such as compilation, NuGet package management and launching a debugger session. It is intended to be fully featured, enabling extensive library and app development functionality appropriate at the command-line. It should provide everything you'd need to develop an app in an SSH session! It is also intended to be a fundamental building block for building finished experiences in tools such as Visual Studio.

Goals:

- Language agnostic - embrace "common language runtime".
- Target agnostic - multi-targets.
- Runtime agnostic.
- Simple extensibility and layering - "you had one job!"
- Cross-platform - support and personality.
- Outside-in philosophy - higher-level tools drive the CLI.

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

**dotnet build**

`dotnet build --native` native compiles your app into a single executable file.

`dotnet build` compiles your app or library as an IL binary. In the case of an app, `build` generates runnable assets by copying an executable host to make the IL binary runable. The host relies on a shared framework for dependencies, including a runtime.

Design
======

There are a couple of moving pieces that you make up the general design of the .NET Core CLI:

* The `dotnet` driver
* Specific commands that are part of the package

The `dotnet` driver is very simple and its primary role is to run commands and give users basic information about usage. 

The way the `dotnet` driver finds the command it is instructed to run using `dotnet {command}` is via a convention; any executable that is placed in the PATH and is named `dotnet-{command}` will be available to the driver. For example, when you install the CLI toolchain there will be an executable called `dotnet-build` in your PATH; when you run `dotnet build`, the driver will run the `dotnet-build` executable. All of the arguments following the command are passed to the command being invoked. So, in the invocation of `dotnet build --native`, the `--native` switch will be passed to `dotnet-build` executable that will do some action based on it (in this case, produce a single native binary).

This is also the basics of the current extensibility model of the toolchain. Any executable found in the PATH named in this way, that is as `dotnet-{command}`, will be invoked by the `dotnet` driver. 

There are some principles that we are using when adding new commands:

* Each command is represented by a verb (`run`, `build`, `publish`, `restore` etc.)
* We support the short and the long form of switches for most commands
* The switches have the same format on all supported platforms (so, no /-style switches on Windows for example)
* Each command has a help that can be viewed by running `dotnet [command] --help`

Adding a new command to the .NET Core CLI 
=========================================

If you want to contribute to the actual .NET Core CLI by adding a new command that you think would be useful, please refer to the [developer guide](developer-guide.md) in this directory. It contains all of the guidance on both the process as well as the infrastructure that you need to adhere to when adding a new command to the CLI toolchain. 

Adding a new command locally
============================ 
Given the extensibility model described above, it is very easy to add a command that can be invoked with the `dotnet` driver. Just add any executable in a PATH and name it as per the instructions above.

As an example, let's say we want to add a local command that will mimic `dotnet clean`. By convention, `dotnet build` will drop binaries in two directories `./bin` and `./obj`. A clean command thus will need to delete these two directories. A trivial example, but it should work.

On *nix OS-es, we will write a very simple shell script to help us with this:
```shell
#!/bin/bash

rm -rf bin/ obj/
```

We then do the following to make it be a command in the CLI toolchain

* Name it as `dotnet-clean`
* Set the executable bit on: `chmod +X dotnet-clean`
* Copy it over somewhere in the $PATH: `sudo cp dotnet-clean /usr/local/bin`

After this, the command ready to be invoked via the `dotnet` driver. 

Guidances on how to write a command 
===================================
How you write a given command depends largely on whether you are trying to add it to the CLI project or want to add the command locally, i.e. on your machine or server. 

For the former case, the [developer guide](developer-guide.md) has all of the details that you will need to get going. 

If you are adding a command on your own machine(s), then there is really no special model to keep in mind. However, since your users will be using the local commands through the `dotnet` driver, we strongly suggest to keep to the principles outlined above in the [design section](#design) to have an unified user experience for your users. 
