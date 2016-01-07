% DOTNET-RUN(1)
% Zlatko Knezevic zlakne@microsoft.com
% January 2016

# NAME 
dotnet-run -- Runs source code 'in-place' without any explicit compile or launch commands.

# SYNOPSIS
`dotnet run [--verbose]`

# DESCRIPTION
The run command provides a convenient option to run source code with one command. It compiles source code, generates an output program and then runs that program. This command is useful for fast iterative development and can also be used to run a source-distributed program (e.g. website).

This command relies on `dotnet-compile(1)` to compile source inputs to a .NET assembly, before launching the program. The requirements for and handling of source inputs for this command are all inherited from the compile command. The documentation for the compile command provides more information on those requirements.

Output files, are written to the child `bin` folder, which will be created if it doesn't exist. Files will be overwritten as needed. Temporary files are written to the child `obj` folder.  

# OPTIONS

-v, --verbose

    Prints verbose logging information, to follow the flow of execution of the command.
    
# ENVIRONMENT 

`DOTNET_HOME`

    Points to the base directory that contains the runtime and the binaries directories. The runtime will be used to run the executable file that is dropped after compiling. Not needed for native compilation.   

# SEE ALSO

dotnet-compile(1), dotnet-publish(1)
