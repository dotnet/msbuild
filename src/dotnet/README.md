% DOTNET(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME

dotnet -- General driver for running the command-line commands

## SYNOPSIS

`dotnet [--version] [--help] [--verbose] [--info] <command> [<args>]`

## DESCRIPTION
`dotnet` is a generic driver for the Command Line Interface (CLI) toolchain. Invoked on its own, it will give out brief usage instructions. 

Each specific feature is implemented as a command. In order to use the feature, the command is specified after `dotnet`, such as [`dotnet build`](https://aka.ms/dotnet-build). All of the arguments following the command are its own arguments. 

The only time `dotnet` is used as a command on its own is to run portable apps. Just specify a portable application DLL after the `dotnet` verb to execute the application.    


## OPTIONS
`-v, --verbose`

Enables verbose output.

`--version`

Prints out the version of the CLI tooling.

`--info`

Prints out more detailed information about the CLI tooling, such as the current operating system, commit SHA for the version, etc. 

`-h, --help`

Prints out a short help and a list of current commands. 

## DOTNET COMMANDS

The following commands exist for dotnet:

* [dotnet-new](https://aka.ms/dotnet-new)
   * Initializes a C# or F# console application project.
* [dotnet-restore](https://aka.ms/dotnet-restore)
  * Restores the dependencies for a given application. 
* [dotnet-build](https://aka.ms/dotnet-build)
  * Builds a .NET Core application.
* [dotnet-publish](https://aka.ms/dotnet-publish)
   * Publishes a .NET portable or self-contained application.
* [dotnet-run](https://aka.ms/dotnet-run)
   * Runs the application from source.
* [dotnet-test](https://aka.ms/dotnet-test)
   * Runs tests using a test runner specified in the project.json.
* [dotnet-pack](https://aka.ms/dotnet-pack)
   * Creates a NuGet package of your code.

## EXAMPLES

`dotnet new`

Initializes a sample .NET Core console application that can be compiled and run.

`dotnet restore`

Restores dependencies for a given application. 

`dotnet compile`

Compiles the application in a given directory. 

`dotnet myapp.dll`

Runs a portable app named `myapp.dll`. 

## ENVIRONMENT 

`NUGET_PACKAGES`

The primary package cache. If not set, it defaults to $HOME/.nuget/packages on Unix or %HOME%\NuGet\Packages on Windows.

`DOTNET_SERVICING`

Specifies the location of the servicing index to use by the shared host when loading the runtime.

`DOTNET_CLI_TELEMETRY_OPTOUT`

Specifies whether data about the .NET Core tools usage is collected and sent to Microsoft. **true** to opt-out of the telemetry feature (values true, 1 or yes accepted); otherwise, **false** (values false, 0 or no accepted). If not set, it defaults to **false**, that is, the telemetry feature is on.
