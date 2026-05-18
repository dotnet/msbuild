MSBuild can be successfully built on Windows, OS X 10.13, Ubuntu 14.04, and Ubuntu 16.04.

# Windows

## Build

`build.cmd -msbuildEngine dotnet`

## Tests

Follow [Running Unit Tests](Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md#running-unit-tests) section of the developer guide chapter for .NET Framework

# Unix

## The easy way

Install the latest .NET SDK from https://dotnet.microsoft.com/download. That will ensure all prerequisites for our build are met.

## Manually installing required packages for OSX & Ubuntu

[.NET Core prerequisites](https://github.com/dotnet/core/blob/main/Documentation/prereqs.md).

* *OpenSSL*: MSBuild uses the .Net CLI during its build process. The CLI requires a recent OpenSSL library available in `/usr/lib`. This can be downloaded using [brew](https://brew.sh/) on OS X (`brew install openssl`) and apt-get (`apt-get install openssl`) on Ubuntu, or [building from source](https://wiki.openssl.org/index.php/Compilation_and_Installation#Mac). If you use a different package manager and see an error that says `Unable to load DLL 'System.Security.Cryptography.Native'`, `dotnet` may be looking in the wrong place for the library.

## Build

`./build.sh`

If you encounter errors, see [Something's wrong in my build](Something's-wrong-in-my-build.md)

## Tests

`./build.sh --test`

# Getting .Net Core MSBuild binaries without building the code

The best way to get .NET Core MSBuild is by installing the [.NET Core SDK](https://github.com/dotnet/core-sdk), which redistributes us. This will get you the latest released version of MSBuild for .NET Core. After installing it, you can use MSBuild through `dotnet build` or by manual invocation of the `MSBuild.dll` in the dotnet distribution.

# Debugging

## Wait in Main

Set the environment variable `MSBUILDDEBUGONSTART` to `2`, then attach a debugger to the process manually after it starts.

## Using the repository binaries to perform builds

To build projects using the MSBuild binaries from the repository, you first need to execute the build command (`build.cmd`). This generates a bootstrap directory that emulates either a Visual Studio environment (full framework version) in the `net472` folder or a .NET Core environment in the `core` folder.

Next, navigate to the `core` folder and run the dotnet executable from this location using the following syntax: `artifacts/bin/bootstrap/core/dotnet.exe <Command> <Project File>`. Replace `<Command>` with any valid dotnet command (such as `build`, `restore`, `test`, etc.) and `<Project File>` with the path to your project file.

See other debugging options [here](./Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md#Debugging-MSBuild).
