% DOTNET-RUN(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME 

dotnet-run -- Runs source code 'in-place' without any explicit compile or launch commands.

## SYNOPSIS

`dotnet run [--framework] [--configuration]
    [--project] [--help] [--]`

## DESCRIPTION
The `dotnet run` command provides a convenient option to run your application from the source code with one command. 
It compiles source code, generates an output program and then runs that program. 
This command is useful for fast iterative development and can also be used to run a source-distributed program (for example, a website).

This command relies on [`dotnet build`](dotnet-build.md) to build source inputs to a .NET assembly, before launching the program. 
The requirements for this command and the handling of source inputs are all inherited from the build command. 
The documentation for the build command provides more information on those requirements.

Output files are written to the child `bin` folder, which will be created if it doesn't exist. 
Files will be overwritten as needed. 
Temporary files are written to the child `obj` folder.  

In case of a project with multiple specified frameworks, `dotnet run` will first select the .NET Core frameworks. If those do not exist, it will error out. To specify other frameworks, use the `--framework` argument.

The `dotnet run` command must be used in the context of projects, not built assemblies. If you're trying to execute a DLL instead, you should use [`dotnet`](dotnet.md) without any command like in the following example:
 
`dotnet myapp.dll`

For more information about the `dotnet` driver, see the [.NET Core Command Line Tools (CLI)](overview.md) topic.


## OPTIONS

`--`

Delimits arguments to `dotnet run` from arguments for the application being run. 
All arguments after this one will be passed to the application being run. 

`-f`, `--framework` [FID]

Runs the application for a given framework identifier (FID). 

`-c`, `--configuration [Debug|Release]`

Configuration to use when publishing. The default value is "Debug".

`-p`, `--project [PATH]`

Specifies which project to run. 
It can be a path to a project.json file or to a directory containing a project.json file. It defaults to
current directory if not specified. 

## EXAMPLES

`dotnet run`

Runs the project in the current directory. 

`dotnet run --project /projects/proj1/project.json`

Runs the project specified.

`dotnet run --configuration Release -- --help`

Runs the project in the current directory. The `--help` argument above is passed to the application being run, since the `--` argument was used.