**These instructions refer to working with the [`xplat`](https://github.com/Microsoft/msbuild/tree/xplat) branch.**

MSBuild can be successfully built on Windows, OS X 10.11, and Ubuntu 14.04. Newer versions of Ubuntu may work, but .NET Core development is currently aimed at 14.04.

# Windows #
##Build process##

`RebuildWithLocalMSBuild.cmd`: This script uses a .NET core hosted MSBuild to target .NET Core and run the tests.

##Debugging##

For debugging tests, use the `/d` switch for CoreRun.exe. This prompts the core CLR to wait for a debugger to attach. You can attach using Visual Studio.

# Unix #

**Required packages for OSX & Ubuntu**

* *OpenSSL*: MSBuild uses the .Net CLI to download Nuget packages during its build process. The CLI requires a recent OpenSSL library available in `/usr/lib`. This can be downloaded using [brew](http://brew.sh/) on OS X (`brew install openssl`) and apt-get (`apt-get install openssl`) on Ubuntu, or [building from source](https://wiki.openssl.org/index.php/Compilation_and_Installation#Mac). If you use a different package manager and see an error that says `Unable to load DLL 'System.Security.Cryptography.Native'`, `dotnet` may be looking in the wrong place for the library.

* [Mono](http://www.mono-project.com/download/#download-lin) when doing a Mono-hosted version of MSBuild

**Required packages for Ubuntu**
* [libunwind](http://www.nongnu.org/libunwind/index.html) is required by .NET Core. Install it using `sudo apt-get install libunwind8`

##Build process##
Clone the xplat branch:
```
git clone git@github.com:Microsoft/msbuild.git --branch xplat 
```

Navigate to the clone's working directory and run your chosen build script:

Targeting .Net Core: `./cibuild.sh --target CoreCLR`

Targeting Mono: `./cibuild.sh --target Mono`

Using a .NET core MSBuild host: `./cibuild.sh --host CoreCLR`

Using a Mono MSBuild host: `./cibuild --host Mono`

Default arguments lead to a Mono hosted MSBuild targeting CoreCLR: `./cibuild.sh`

##Debugging##
To get a clearer idea of what's going on in your build, check out [MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog). MSBuildStructuredLog is a logger for MSBuild that records and visualizes a structured representation of executed targets, tasks, properties, and item values.

![](https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/docs/Screenshot1.png) 

More debugging tools listed [here](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/MSBuild-Resources#tools).

##Tests##

Tests are currently disabled on platforms other than Windows. If you'd like to run them, explicitly opt in with
```sh
./cibuild.sh --scope Test
```

## Unofficial: Getting .Net Core MSBuild binaries without building the code ##
This is a non-ideal, intermediary solution for getting .NET core MSBuild binaries. We plan on improving this experience.

```
git clone https://github.com/Microsoft/msbuild.git
cd msbuild
git fetch --all
git checkout origin/xplat
init-tools.cmd # windows
./init-tools.sh # unix

./Tools/dotnetcli/dotnet ./Tools/MSBuild.exe /path/to/project
```

