Developer Guide
===============

## Prerequisites

In order to build .NET Command-line Interface (CLI), you need the following installed on you machine:

### For Windows

1. git (available from http://www.git-scm.com/) on the PATH.

### For Linux

1. git (available from http://www.git-scm.com/) on the PATH.

### For OS X

1. Xcode
2. git (available from http://www.git-scm.com/) on the PATH.
3. Install OpenSSL (a .NET Core requirement)
  - brew update
  - brew install openssl
  - mkdir -p /usr/local/lib
  - ln -s /usr/local/opt/openssl/lib/libcrypto.1.0.0.dylib /usr/local/lib/
  - ln -s /usr/local/opt/openssl/lib/libssl.1.0.0.dylib /usr/local/lib/

## Building/Running

1. Run `build.cmd` or `build.sh` from the root depending on your OS. If you don't want to execute tests, run `build.cmd /t:Compile` or `./build.sh /t:Compile`. 
2. The CLI that is built (we call it stage 2) is laid out in the `bin\2\{RID}\dotnet` folder. You can run `dotnet.exe` or `dotnet` from that folder to try out the `dotnet` command.

> If you need to update localizable strings in resource (*.resx*) files, run `build.cmd /p:UpdateXlfOnBuild=true` or `./build.sh /p:UpdateXlfOnBuild=true` to update the XLIFF (*.xlf*) files as well.

## A simple test
Using the `dotnet` built in the previous step:

1. `cd {new directory}`
2. `dotnet new`
3. `dotnet restore`
4. `dotnet run`

## Running tests

1. To run all tests, invoke `build.cmd` or `build.sh` which will build the product and run the tests.
2. To run a specific test project:
    - Run `scripts\cli-test-env.bat` on Windows, or [source](https://en.wikipedia.org/wiki/Source_(command)) `scripts/cli-test-env.sh` on Linux or OS X.  This will add the stage 2 `dotnet` folder to your path and set up other environment variables which are used for running tests.
    - `cd` into the test's directory
    - Run `dotnet test`
    - Refer to the command-line help for `dotnet test` if you want to run a specific test in the test project

## Adding a Command

The dotnet CLI supports several models for adding new commands:

1. In the CLI itself via `dotnet.dll`
2. Through a `tool` NuGet package
3. Through MSBuild tasks & targets in a NuGet package
4. Via the user's `PATH`

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
