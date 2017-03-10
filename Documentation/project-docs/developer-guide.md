Developer Guide
===============

## Prerequisites

In order to build .NET Command Line Interface, you need the following installed on you machine.

### For Windows

1. CMake (available from https://cmake.org/) on the PATH.
2. git (available from http://www.git-scm.com/) on the PATH.

### For Linux

1. CMake (available from https://cmake.org/) is required to build the native host `corehost`. Make sure to add it to the PATH.
2. git (available from http://www.git-scm.com/) on the PATH.
3. clang (available from http://clang.llvm.org) on the PATH.

### For OS X

1. Xcode
2. CMake (available from https://cmake.org/) on the PATH.
3. git (available from http://www.git-scm.com/) on the PATH.
4. Install OpenSSL (a .NET Core requirement)
  - brew install openssl
  - brew link --force openssl

## Building/Running

1. Run `build.cmd` or `build.sh` from the root depending on your OS. If you don't want to execute tests, run `build.cmd /t:Compile` or `./build.sh /t:Compile`. 
  - To build the CLI in macOS Sierra, you need to set the DOTNET_RUNTIME_ID environment variable by running `export DOTNET_RUNTIME_ID=osx.10.11-x64`.
2. Use `artifacts/{RID}/stage2/dotnet` to try out the `dotnet` command. You can also add `artifacts/{os}-{arch}/stage2` to the PATH if you want to use the build output when invoking `dotnet` from the current console.

## A simple test
Using the `dotnet` built in the previous step:

1. `cd {new directory}`
2. `dotnet new`
3. `dotnet restore3`
4. `dotnet run3`

## Running tests

1. To run all tests invoke `build.cmd` or `build.sh` which will build the product and run the tests.
2. To run a specific test, cd into that test's directory and execute `dotnet test`. If using this approach, make sure to add `artifacts/{RID}/stage2` to your `PATH` and set the `NUGET_PACKAGES` environment variable to point to the repo's `.nuget/packages` directory.

##Adding a Command

The dotnet CLI supports several models for adding new commands:

0. In the CLI itself via `dotnet.dll`
1. Through a `tool` NuGet package
2. Through MSBuild tasks & targets in a NuGet package
3. Via the user's `PATH`

### Commands in dotnet.dll
Developers are generally encouraged to avoid adding commands to `dotnet.dll` or the CLI installer directly. This is appropriate for very general commands such as restore, build, publish, test, and clean, but is generally too broad of a distribution mechanism for new commands. Please create an issue and engage the team if you feel there is a missing core command that you would like to add.

### Tools NuGet packages
Many existing extensions, including those for ASP.NET Web applications, extend the CLI using Tools NuGet packages. For an example of a working packaged command look at `TestAssets/TestPackages/dotnet-hello/v1/`.

### MSBuild tasks & targets
NuGet allows adding tasks and targets to a project through a NuGet package. This mechanism, in fact, is how all .NET Core projects pull in the .NET SDK. Extending the CLI through this model has several advantages:

1. Targets have access to the MSBuild Project Context, allowing them to reason about the files and properties being used to build a particular project.
2. Targets are not CLI-specific, making them easy to share across command-line and IDE environments

Commands added as targets can be invoked once the target project adds a reference to the containing NuGet package and restores. 
Targets are invoked by calling `dotnet msbuild /t:{TargetName}`

### Commands on the PATH
The dotnet CLI considers any executable on the path named `dotnet-{commandName}` to be a command it can call out to. 

## Things to Know
- Any added commands are usually invoked through `dotnet {command}`. As a result of this, stdout and stderr are redirected through the driver (`dotnet`) and buffered by line. As a result of this, child commands should use Console.WriteLine in any cases where they expect output to be written immediately. Any uses of Console.Write should be followed by Console.WriteLine to ensure the output is written.
