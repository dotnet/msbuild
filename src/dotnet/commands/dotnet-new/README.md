% DOTNET-NEW(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME
dotnet-new -- Create a new sample .NET Core project

## SYNOPSIS
dotnet new [--type] [--lang]

## DESCRIPTION
The `dotnet new` command provides a convenient way to initialize a valid .NET Core project and sample source code to try out the Command Line Interface (CLI) toolset. 

This command is invoked in the context of a directory. When invoked, the command will result in two main artifacts being dropped to the directory: 

1. A `Program.cs` (or `Program.fs`) file that contains a sample "Hello World" program.
2. A valid `project.json` file.

After this, the project is ready to be compiled and/or edited further. 

## Options

`-l`, `--lang [C#|F#]`

Language of the project. Defaults to `C#`. `csharp` (`fsharp`) or `cs` (`fs`) are also valid options.

`-t`, `--type`

Type of the project. Valid values for C# are:

* `console`
* `web`
* `lib`
* `xunittest`

Valid values for F# are:

* `console`
* `lib`

## EXAMPLES

`dotnet new`
    
    Drops a sample C## project in the current directory.

`dotnet new --lang f##`
    
    Drops a sample F## project in the current directory.

`dotnet new --lang c##`
    
    Drops a sample C## project in the current directory.

# SEE ALSO
dotnet-run(1)
