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

`./build.sh --test` runs all tests in the repo.

# Getting .Net Core MSBuild binaries without building the code

The best way to get .NET Core MSBuild is by installing the [.NET Core SDK](https://github.com/dotnet/core-sdk), which redistributes us. This will get you the latest released version of MSBuild for .NET Core. After installing it, you can use MSBuild through `dotnet build` or by manual invocation of the `MSBuild.dll` in the dotnet distribution.

# Debugging

## Wait in Main

Set the environment variable `MSBUILDDEBUGONSTART` to control debugger behavior at startup:
- `1`: Launch debugger for all processes (main MSBuild and TaskHost child processes)
- `2`: Wait for manual debugger attach for all processes
- `3`: Launch debugger for main MSBuild process only, skip TaskHost child processes

For example, set `MSBUILDDEBUGONSTART` to `2`, then attach a debugger to the process manually after it starts.

## Debugging MSBuild Server

With Server enabled, use `MSBUILDDEBUGONSTART=4` to launch a debugger in the server (Windows), or `5` to wait for manual attachment. These modes skip the forwarding client and other child nodes, and pause **after connection but before evaluation**, on either a new or reused server.

```powershell
$env:MSBUILDUSESERVER = '1'
$env:MSBUILDDEBUGONSTART = '5'
dotnet msbuild .\project.proj
```

Attach to the server PID printed in the terminal and resume execution; unlike mode `2`, no Enter key is needed. No connection or handshake changes are required because the client is already connected. Existing cancellation/fallback behavior is unchanged: resume or stop the server manually to abandon the wait. Unset `MSBUILDDEBUGONSTART` when finished. These modes do not cover server startup or handshake debugging.

## Using the repository binaries to perform builds

## Run tests from the command line

```shell
build.cmd # to have a full build first
cd test\YOURTEST.Tests # cd to the test folder that contains the test csproj file
dotnet test --filter "FullyQualifiedName~TESTNAME" # run individual test
```

## Run tests in Visual Studio

Use a `Developer Command Prompt for VS 2026` or put `devenv` on your `PATH`.

```shell
build.cmd # to have a full build first
devenv MSBuild.slnx
```

Note again that in Visual studio "Use previews of the .NET SDK (requires restart)" must be checked. See the above comment for how to enable this.

See other debugging options [here](./Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md#Debugging-MSBuild).