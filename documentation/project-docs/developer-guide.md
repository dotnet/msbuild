Developer Guide
===============

## Prerequisites

In order to build and test the .NET Core Command-line Interface (CLI), you need the following installed on you machine:

### For Windows

1. git (available from the [Git Website](http://www.git-scm.com/)) on the PATH.

### For Linux

1. git (available from your package manager or the [Git Website](http://www.git-scm.com/)) on the PATH.

### For macOS

1. git (available from [Homebrew](https://www.google.com/search?client=firefox-b-1-d&q=homebrew) or the [Git Website](http://www.git-scm.com/)) on the PATH.

## Building

### Windows

Run the following command from the root of the repository:

```
build.cmd
```

The build script will output a `dotnet` installation to `artifacts\tmp\Debug\dotnet` that will include any local changes to the .NET Core CLI.

To open the solution in Visual Studio, be sure to build with `build.cmd` and run the generated `artifacts\sdk-build-env.bat`. Finally, open Visual Studio with `devenv sdk.sln`.

### Linux and macOS

Run the following command from the root of the repository:

```
./build.sh
```

The build script will output a .NET Core installation to `artifacts/tmp/Debug/dotnet` that will include any local changes to the .NET Core CLI.

## Running tests

### Windows

Run the following command from the root of the repository to run all the .NET Core CLI tests:

```
build.cmd -test
```

### Linux and macOS

Run the following command from the root of the repository to run all the .NET Core CLI tests:

```
./build.sh --test
```

## Using the built dotnet

The `dotnet` executable in the artifacts directory can be run directly.

However, it's easier to configure a test environment to run the built `dotnet`.

### Windows

Run the following commands from the root of the repository to setup the test environment:

```
eng\dogfood.cmd
```

Ensure the `dotnet` being used is from the artifacts directory:

```
where dotnet
```

This should output `..\artifacts\tmp\Debug\dotnet\dotnet.exe`.

You can now run `dotnet` commands to test changes.

### Linux and macOS

Run the following commands from the root of the repository to setup the test environment:

```
.\eng\dogfood.sh
```

Ensure the `dotnet` being used is from the artifacts directory:

```
which dotnet
```

This should output `.../artifacts/tmp/Debug/dotnet/dotnet`.

You can now run `dotnet` commands to test changes.

## A simple test

Using the `dotnet` built in the previous steps:

```
mkdir test
cd test
dotnet new console
dotnet run
```

This should print `Hello World!`.

## Adding a Command

The dotnet CLI supports several models for adding new commands:

1. In the CLI itself via `dotnet.dll`.
2. Through a [.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
3. Through MSBuild tasks & targets in a NuGet package.
4. Through executables prefixed with `dotnet-` on the `PATH`.

### Commands in dotnet.dll

Developers are generally encouraged to avoid adding commands to `dotnet.dll` or the CLI installer directly. This is appropriate for very general commands such as `restore`, `build`, `publish`, `test`, and `clean`, but is generally too broad of a distribution mechanism for new commands.

Create an issue and engage the repository maintainers if you feel there is a missing core command that you would like to add.

### .NET Core Global Tools

A [.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) can be used to install a `dotnet-` prefixed tool to the `PATH`.

The CLI will then treat the remainder of the tool name as a `dotnet` command.  For example, a tool with the name `dotnet-hello` could be executed by `dotnet hello`.

### MSBuild tasks & targets

NuGet allows adding tasks and targets to a project through a NuGet package.  Extending the CLI through this model has several advantages:

1. Targets have access to the MSBuild Project Context, allowing them to reason about the files and properties being used to build a particular project.
2. Targets are not CLI-specific, making them easy to share across command-line and IDE environments

Commands added as targets can be invoked once the target project adds a reference to the containing NuGet package and restores.

Targets are invoked by calling `dotnet msbuild /t:{TargetName}`

### Commands on the PATH

The dotnet CLI considers any executable on the path named `dotnet-{commandName}` to be a command it can call out to.
