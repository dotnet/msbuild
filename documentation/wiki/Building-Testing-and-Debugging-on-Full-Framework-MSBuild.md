# Building MSBuild for the .NET Framework

These instructions refer to working with the `master` branch.

## Required Software

**Latest Microsoft Visual Studio 2017**: You can download the Visual Studio Community edition from [https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx).

All command lines should be executed from a Visual Studio developer command prompt.

## Getting the code

1. Clone the repo: `git clone https://github.com/Microsoft/msbuild.git`
2. Build on the command line: `build.cmd`
3. Open the solution file in Visual Studio (`MSBuild.dev.sln`).

## Running Unit Tests

To run the unit tests from Visual Studio:

1. Open the MSBuild solution file (`MSBuild.dev.sln`) in Visual Studio.
2. Open the Test menu -> Windows -> Test Explorer.
3. Click Run All.

To build MSBuild and run all unit tests from the command line, use `build.cmd -test`.

To mimic our CI job use `build\cibuild.cmd`. Be aware that this command deletes your nuget cache. You can temporarily edit the script `build\build.ps1` to prevent it from deleting the cache.

The CI does two builds. In the second build, it uses the binaries from the first build to build the repository again.

## Contributing

Please see [Contributing Code](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/Contributing-Code.md) for details on contributing changes back to the code. Please read this carefully and engage with us early to ensure work is not wasted.

## Walkthroughs

### Debugging MSBuild

- Breaking into the main method of MSBuild.exe: set the environment variable `MSBUILDDEBUGONSTART` to 1 or 2: https://github.com/Microsoft/msbuild/blob/master/src/MSBuild/XMake.cs#L488-L501
- Dumping scheduler state: set `MSBUILDDEBUGSCHEDULER` to 1; set `MSBUILDDEBUGPATH` to a directory to dump the scheduling state files.

### Using the repository binaries to perform builds

To build projects using the MSBuild binaries from the repository, you first need to do a build (command: `build.cmd`) which produces a bootstrap directory mimicing a Visual Studio installation.

Now, just point `artifacts\Debug\bootstrap\net472\MSBuild\15.0\Bin\MSBuild.exe` at a project file.
