% DOTNET-NEW(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME
dotnet-new -- Create a new sample .NET Core project

## SYNOPSIS
dotnet-new [--type] [--lang]

## DESCRIPTION
The new command provides a convenient way to initalize a valid .NET Core project and sample source code to try out the CLI toolset. 

This command is invoked in the context of a directory. When invoked, the command will result in two main artifacts being dropped to the directory: 

1. A sample "Hello World" program that exists in `Program.cs` ( or `Program.fs` ) file.
2. A valid `project.json` file

> **Note:** As a workaround for packages not being on NuGet.org yet (since this is prelease software) the `dotnet-new`
> command will also drop a `NuGet.config` file. This will be removed at RC2 release. 

After this, the project is ready to be compiled and/or edited further. 

## Options

`-l`, `--lang [C##|F##]`

Language of project. Defaults to `C##`. Also `csharp` ( `fsharp` ) or `cs` ( `fs` ) works.

`-t`, `--type`

Type of the project. Valid value is "console".

## EXAMPLES

`dotnet new`
    
    Drops a sample C## project in the current directory.

`dotnet new --lang f##`
    
    Drops a sample F## project in the current directory.

`dotnet new --lang c##`
    
    Drops a sample C## project in the current directory.

# SEE ALSO
dotnet-run(1)
