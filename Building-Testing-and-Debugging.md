# Building, Testing, and Debugging

## Required Software
**Microsoft Visual Studio Ultimate 2015 Preview**

This version of MSBuild closely aligns to the version that ships with Visual Studio 2015. You may be able to build and debug with Visual Studio 2013, but using the latest preview release is recommended. You can download the preview release (**CTP6**) for free from [http://www.visualstudio.com/downloads/visual-studio-2015-ctp-vs](http://www.visualstudio.com/downloads/visual-studio-2015-ctp-vs).
> Please note this is intending as a standalone build engine, not integrated with Visual Studio. We may add support/documentation for that scenario if we see community interest for it.

## Getting the code

1. Clone the repo: `git clone https://github.com/Microsoft/msbuild.git`
2. Open the solution file in Visual Studio 2015 (`src/MSBuild.sln`).

# Running Unit Tests
To run the unit tests from Visual Studio:

1. Open the MSBuild solution file (`src/MSBuild.sln`) in Visual Studio 2015.
2. Open the Test menu -> Windows -> Test Explorer.
3. Click Run All.

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
Please note that at this time the `RebuildWithLocalMSBuild.cmd` script will build the solution and package with it dependencies installed on your machine found under `C:\Program Files (x86)\MSBuild\`. We intend to move these dependencies to NuGet packages at some point, but for now this is the primary reason Visual Studio 2015 is required.

## Build a Console App
To build a console app, you first need a drop of MSBuild (built on your machine) with all the required dependencies. To do this, open a 'Developer Command Prompt for VS2015' and run the following command from your msbuild folder:
```
BuildAndCopy.cmd bin\MSBuild true
``` 
> If you're interested in this command and the 'true' flag, see [Microsoft.Build.Framework wiki page](https://github.com/Microsoft/msbuild/wiki/Microsoft.Build.Framework) for a complete explanation.

Now, just point `bin\MSBuild\MSBuild.exe` at a project file. Here's a quick sample project that will build an application that runs on the .NET Core framework:
```
cd ..\
git clone https://github.com/dotnet/corefxlab
.\msbuild\bin\MSBuild\MSBuild.exe .\corefxlab\demos\CoreClrConsoleApplications\HelloWorld\HelloWorld.csproj
.\corefxlab\demos\CoreClrConsoleApplications\HelloWorld\bin\Debug\HelloWorld.exe
```
>Paths here assumes corefxlab and msbuild repos are in the same parent folder.
