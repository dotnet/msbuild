**These instructions refer to working with the Master branch.**

## Required Software
**Microsoft Visual Studio 2015 **

This version of MSBuild closely aligns to the version that ships with Visual Studio 2015. You may be able to build and debug with Visual Studio 2013, but using Visual Studio 2015 is recommended. You can download the community edition from [https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx).

> MSBuild requires that you have the Windows SDK plugin installed with Visual Studio 2015. Make sure you have the plugin selected when installing Visual Studio. You can also modify your existing VS installation by running the installer again. ![](https://cloud.githubusercontent.com/assets/3347530/10229970/69396342-6840-11e5-8ef6-1f4434c4b36f.png)

> Please note this is intending as a standalone build engine, not integrated with Visual Studio. We may add support/documentation for that scenario if we see community interest for it.

## Getting the code

1. Clone the repo: `git clone https://github.com/Microsoft/msbuild.git`
2. Build on the command line: `cibuild.cmd --target Full --scope Compile --bootstrap-only`
3. Open the solution file in Visual Studio 2015 (`src/MSBuild.sln`).

# Running Unit Tests
To run the unit tests from Visual Studio:

1. Open the MSBuild solution file (`src/MSBuild.sln`) in Visual Studio 2015.
2. Open the Test menu -> Windows -> Test Explorer.
3. Click Run All.

To build MSBuild and run all unit tests, use `RebuildWithLocalMSBuild.cmd` as described in "Build and verify MSBuild" below. That is usually the best way to ensure that a change is ready to go.

# Contributing
Please see [Contributing Code](https://github.com/Microsoft/msbuild/wiki/Contributing-Code) for details on contributing changes back to the code. Please read this carefully and engage with us early to ensure work is not wasted.

# Walkthroughs
## Build and verify MSBuild
The first scenario you might want to try is building our source tree and then using that output to build it again. To do this, you will need to have Visual Studio 2015 installed on your machine. First, open a 'Developer Command Prompt for VS2015':
```
git clone https://github.com/Microsoft/msbuild.git
cd .\msbuild
.\build.cmd
.\RebuildWithLocalMSBuild.cmd
```

## Debugging MSBuild 
- Breaking into the main method of MSBuild.exe: set the environment variable `MSBUILDDEBUGONSTART` to 1 or 2: https://github.com/Microsoft/msbuild/blob/master/src/MSBuild/XMake.cs#L488-L501
- Dumping scheduler state: set `MSBUILDDEBUGSCHEDULER` to 1; set `MSBUILDDEBUGPATH` to where to dump the scheduling state
- Example of manually running a single unit test:
```
packages\xunit.runner.console\2.1.0\tools\xunit.console.x86.exe bin\Debug\x86\Windows_NT\Windows_NT_Deployment_Test\Microsoft.Build.Engine.UnitTests.dll -noshadow -method Microsoft.Build.UnitTests.Evaluation.ItemEvaluation_Tests.ImmutableListBuilderBug
```

## Build a Console App
To build a console app, you first need a drop of MSBuild (built on your machine) with all the required dependencies. To do this, open a 'Developer Command Prompt for VS2015' and run the following command from your msbuild folder:
```
BuildAndCopy.cmd bin\MSBuild
``` 
Now, just point `bin\MSBuild\MSBuild.exe` at a project file. Here's a quick sample project that will build an application that runs on the .NET Core framework:
```
cd ..\
git clone https://github.com/dotnet/corefxlab
.\msbuild\bin\MSBuild\MSBuild.exe .\corefxlab\demos\CoreClrConsoleApplications\HelloWorld\HelloWorld.csproj
.\corefxlab\demos\CoreClrConsoleApplications\HelloWorld\bin\Debug\HelloWorld.exe
```
>Paths here assumes corefxlab and msbuild repos are in the same parent folder.
