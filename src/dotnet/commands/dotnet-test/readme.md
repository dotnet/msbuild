% DOTNET-TEST(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME

`dotnet-test` - Runs unit tests using the configured test runner

## SYNOPSIS

`dotnet test [--configuration]  
    [--output] [--build-base-path] [--framework] [--runtime]
    [--no-build]
    [--parentProcessId] [--port]  
    [<project>]`  

## DESCRIPTION

The `dotnet test` command is used to execute unit tests in a given project. Unit tests are class library 
projects that have dependencies on the unit test framework (for example, NUnit or xUnit) and the 
dotnet test runner for that unit testing framework. 
These are packaged as NuGet packages and are restored as ordinary dependencies for the project.

Test projects also need to specify a test runner property in project.json using the "testRunner" node. 
This value should contain the name of the unit test framework.

The following sample project.json shows the properties needed:

```json
{
    "version": "1.0.0-*",

    "dependencies": {
        "Microsoft.NETCore.App": {
            "version": "1.0.0-rc2-3002702",
            "type": "platform"
        },
        "xunit": "2.1.0",
        "dotnet-test-xunit": "1.0.0-rc2-build10015"
    },
    "testRunner": "xunit",

    "frameworks": {
        "netcoreapp1.0": {
                "imports": [
                    "dnxcore50",
                    "portable-net45+win8"
                ]
        }
    }
}
```
`dotnet test` supports two running modes:

1. Console: In console mode, `dotnet test` simply executes fully any command gets passed to it and outputs the results. Anytime you invoke `dotnet test` without passing --port, it runs in console mode, which in turn will cause the runner to run in console mode.
2. Design time: used in the context of other tools, such as editors or Integrated Development Environments (IDEs). You can find out more about this mode in the [dotnet-test protocol](../../../../Documentation/dotnet-test-protocol.md) document. 

## OPTIONS

`[project]`
    
Specifies a path to the test project. If omitted, it defaults to current directory. 

`-c`, `--configuration` [Debug|Release]

Configuration under which to build. The default value is Release. 

`-o`, `--output` [DIR]

Directory in which to find binaries to run.

`-b`, `--build-base-path` [DIR]

Directory in which to place temporary outputs.

`-f`, `--framework` [FRAMEWORK]

Looks for test binaries for a specific framework.

`-r`, `--runtime` [RUNTIME_IDENTIFIER]

Look for test binaries for a for the specified runtime.

`--no-build` 

Does not build the test project prior to running it. 

--parentProcessId

Used by IDEs to specify their process ID. Test will exit if the parent process does.

`--port`

Used by IDEs to specify a port number to listen for a connection.

## EXAMPLES

`dotnet test`

Runs the tests in the project in the current directory. 

`dotnet test /projects/test1/project.json`

Runs the tests in the test1 project. 
