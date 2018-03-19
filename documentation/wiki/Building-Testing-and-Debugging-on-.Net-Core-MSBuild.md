MSBuild can be successfully built on Windows, OS X 10.11, Ubuntu 14.04, and Ubuntu 16.04. Newer versions of Ubuntu may work, but .NET Core development is currently aimed at 14.04.

# Windows #
## Build process

`cibuild.cmd --target CoreCLR`

# Unix #

**Required packages for OSX & Ubuntu**

MSBuild currently builds with a prerelease version of .NET Core 1.0. It requires the [.NET Core prerequisites](https://github.com/dotnet/core/blob/master/Documentation/prereqs.md), which you can acquire manually or easily get by [installing the .NET Core SDK](https://dot.net/core).

* *OpenSSL*: MSBuild uses the .Net CLI to download Nuget packages during its build process. The CLI requires a recent OpenSSL library available in `/usr/lib`. This can be downloaded using [brew](http://brew.sh/) on OS X (`brew install openssl`) and apt-get (`apt-get install openssl`) on Ubuntu, or [building from source](https://wiki.openssl.org/index.php/Compilation_and_Installation#Mac). If you use a different package manager and see an error that says `Unable to load DLL 'System.Security.Cryptography.Native'`, `dotnet` may be looking in the wrong place for the library.

* [Mono](http://www.mono-project.com/download/#download-lin) when doing a Mono-hosted version of MSBuild

**Required packages for Ubuntu**
* [libunwind](http://www.nongnu.org/libunwind/index.html) is required by .NET Core. Install it using `sudo apt-get install libunwind8`

##Build process##

Targeting .Net Core: `./cibuild.sh --target CoreCLR`

Targeting Mono: `./cibuild.sh --target Mono`

Using a .NET core MSBuild host: `./cibuild.sh --host CoreCLR`

Using a Mono MSBuild host: `./cibuild --host Mono`

##Tests##

Tests are currently disabled on platforms other than Windows. If you'd like to run them, explicitly opt in with
```sh
./cibuild.sh --scope Test
```

## Getting .Net Core MSBuild binaries without building the code ##
The best way to get .NET Core MSBuild is through the [dotnet CLI](https://github.com/dotnet/cli/), which redistributes us. It's not always the very very latest but they take regular drops. After installing it, you can use MSBuild through `dotnet build` or by manual invocation of the `MSBuild.dll` in the dotnet distribution.

## Debugging

### Wait in Main
Set the environment variable `MSBUILDDEBUGONSTART` to 2.

### Debugging a test
Add a `Console.ReadKey` in the test and manually invoke it via xunit. Example:
```
Tools\dotnetcli\dotnet.exe bin\Debug-NetCore\AnyCPU\Windows_NT\Windows_NT_Deployment_Test\xunit.console.netcore.exe bin\Debug-NetCore\AnyCPU\Windows_NT\Windows_NT_Deployment_Test\Microsoft.Build.Engine.UnitTests.dll -noshadow -method Microsoft.Build.UnitTests.Evaluation.ItemEvaluation_Tests.ImmutableListBuilderBug
```