% DOTNET-RUN(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME 

dotnet-run -- Runs source code 'in-place' without any explicit compile or launch commands.

## SYNOPSIS

dotnet-run [--framework] [--configuration]
    [--project] [--] [--help]

## DESCRIPTION
The run command provides a convenient option to run source code with one command. It compiles source code, generates an 
output program and then runs that program. This command is useful for fast iterative development and can also be used 
to run a source-distributed program (e.g. website).

This command relies on `dotnet-build(1)` to build source inputs to a .NET assembly, before launching the program. 
The requirements for and handling of source inputs for this command are all inherited from the build command. 
The documentation for the build command provides more information on those requirements.

Output files are written to the child `bin` folder, which will be created if it doesn't exist. 
Files will be overwritten as needed. Temporary files are written to the child `obj` folder.  

In case of a project with multiple specified frameworks, `dotnet run` will first select the .NET Core frameworks. If 
those do not exist, it will error out. To specify other frameworks, use the `--framework` argument. 

## OPTIONS

`--`

Delimit arguments to `dotnet run` from arguments for the application being run. All arguments after this one will be passed to
the application being run. 

`-f`, `--framework` [FID]

Run the application for a given framework identifier (FID). 

`-c`, `--configuration [Debug|Release]`

Configuration to use when publishing. If not specified, will default to "Debug".

`-p`, `--project [PATH]`

Specifies which project to run. Can be a path to project.json or to a directory containing a project.json. Defaults to
current directory if not specified. 


# SEE ALSO

dotnet-build(1), dotnet-publish(1)
