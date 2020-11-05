# Building MSBuild for the .NET Framework

These instructions refer to working with the `master` branch.

## Required Software

**Latest Microsoft Visual Studio 2019**: You can download the Visual Studio Community edition from [https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx).

All command lines should be executed from a Visual Studio developer command prompt.

## Getting the code

1. Clone the repo: `git clone https://github.com/Microsoft/msbuild.git`
2. Build on the command line: `.\build.cmd`
   1. If you encounter errors, see [Something's wrong in my build](Something's-wrong-in-my-build.md).
3. Open the solution file in Visual Studio (`MSBuild.Dev.sln`).

## Running Unit Tests

To run the unit tests from Visual Studio:

1. Open the MSBuild solution file (`MSBuild.Dev.sln`) in Visual Studio.
2. Open the Test menu -> Windows -> Test Explorer.
3. Click Run All.

To build MSBuild and run all unit tests from the command line, use `.\build.cmd -test`.

To mimic our CI job use `eng\CIBuild.cmd`. Be aware that this command may delete your local NuGet cache.

The CI does two builds. In the second build, it uses the binaries from the first build to build the repository again.

## Contributing

Please see [Contributing Code](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/Contributing-Code.md) for details on contributing changes back to the code. Please read this carefully and engage with us early to ensure work is not wasted.

## Walkthroughs

### Debugging MSBuild

- Breaking into the main method of MSBuild.exe: set the environment variable `MSBUILDDEBUGONSTART` to 1 or 2: https://github.com/Microsoft/msbuild/blob/master/src/MSBuild/XMake.cs#L488-L501
- Dumping scheduler state: set `MSBUILDDEBUGSCHEDULER` to 1; set `MSBUILDDEBUGPATH` to a directory to dump the scheduling state files.

### Using the repository binaries to perform builds

To build projects using the MSBuild binaries from the repository, you first need to do a build which produces
a "bootstrap" directory. The "bootstrap" directory mimics a Visual Studio installation by aquiring additional
dependencies (Roslyn compilers, NuGet, etc.) from packages or from your local machine (e.g. props/targets
from Visual Studio). To produce a bootstrap build, run `.\build.cmd /p:CreateBootstrap=true` from the root of your enlistment.

Now, just point `artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\MSBuild.exe` at a project file.

### Patching Visual Studio

Sometimes it's useful to patch your copy of Visual Studio. You can use the [Deploy-MSBuild script](../Deploy-MSBuild.md) for that.
