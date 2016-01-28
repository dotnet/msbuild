% DOTNET(1)
% Zlatko Knezevic zlakne@microsoft.com
% January 2016

# NAME

dotnet -- general driver for running the command-line commands

# SYNOPSIS

dotnet [--version] [--help] [--verbose] < command > [< args >]

# DESCRIPTION
dotnet is a generic driver for the CLI toolchain. Invoked on its own, it will give out brief usage instructions. 

Each specific feature is implemented as a command. In order to use the feature, it is specified after dotnet, i.e. `dotnet compile`. All of the arguments following the command are command's own arguments.  


# OPTIONS
`-v, --verbose`

    Enable verbose output.

`--version`

    Print out the version of the CLI tooling

`-h, --help`

    Print out a short help and a list of current commands. 

# DOTNET COMMANDS

The following commands exist for dotnet.

`dotnet-compile(1)`

    Compile the application to either an intermidiate language (IL) or to a native binary. 

`dotnet-restore(1)`

    Restores the dependencies for a given application. 

`dotnet-run(1)`

    Runs the application from source.

`dotnet-publish(1)`

    Publishes a flat directory that contains the application and its dependencies, including the runtime binaries. 

`dotnet-test(1)`

    Runs tests using a test runner specified in project.json.

`dotnet-new(1)`

    Initializes a sample .NET Core console application. 

# EXAMPLES

`dotnew new`

    Initializes a sample .NET Core console application that can be compiled and ran.

`dotnet restore`

    Restores dependencies for a given application. 

`dotnet compile`

    Compiles the application in a given directory. 

# ENVIRONMENT 

`DOTNET_HOME`

    Points to the base directory that contains the runtime and the binaries directories. The runtime will be used to run the executable file that is dropped after compiling. Not needed for native compilation.  

`DOTNET_PACKAGES`

    The primary package cache. If not set, defaults to $HOME/.nuget/packages on Unix or %LOCALAPPDATA%\NuGet\Packages (TBD) on Windows.

`DOTNET_PACKAGES_CACHE`

    The secondary cache. This is used by shared hosters (such as Azure) to provide a cache of pre-downloaded common packages on a faster disk. If not set it is not used.

`DOTNET_SERVICING`

    Specifies the location of the servicing index to use by the shared host when loading the runtime. 

# SEE ALSO
dotnet-compile(1), dotnet-run(1), dotnet-publish(1), dotnet-restore(1)
