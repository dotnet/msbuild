**These instructions refer to working with the [`xplat`](https://github.com/Microsoft/msbuild/tree/xplat) branch.**

MSBuild can be successfully built on Windows, OS X, and Ubuntu.

## Windows ##
**Build process**

`RebuildWithLocalMSBuild.cmd`. This script uses a .NET core hosted MSBuild to target .NET Core and run the tests.

**Debugging**

For debugging tests, use the `/d` switch for CoreRun.exe. This prompts the core CLR to wait for a debugger to attach. You can attach using Visual Studio.

## Unix ##

**Required packages**

_Mono_, when doing a Mono-hosted version of MSBuild

**Build process**

Targeting .Net Core: `./cibuild.sh --target CoreCLR`

Targeting Mono: `./cibuild.sh --target Mono`

Using a .NET core MSBuild host: `./cibuild.sh --host CoreCLR`

Using a Mono MSBuild host: `./cibuild --host Mono`

Default arguments lead to a Mono hosted MSBuild targeting CoreCLR: `./cibuild.sh`

**Tests**

Tests are currently disabled on platforms other than Windows. If you'd like to run them, explicitly opt in with
```sh
./cibuild.sh --scope Test
```

**Debugging**

TBD

## Unofficial: Getting .Net Core MSBuild binaries without building the code ##
This is a non-ideal, intermediary solution for getting .NET core MSBuild binaries. We plan on improving this experience.

```
git clone https://github.com/Microsoft/msbuild.git
cd msbuild
git fetch --all
git checkout origin/xplat
init-tools.cmd # windows
./init-tools.sh # unix

./Tools/corerun ./Tools/MSBuild.exe /path/to/project
```

