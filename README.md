# .NET Command Line Interface

Build Status
------------

|         |Ubuntu 14.04 |Windows |Mac OS X |
|---------|:------:|:------:|:------:|
|**Debug**|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_ubuntu)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_ubuntu/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_windows_nt)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_windows_nt/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_osx)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_osx/) |
|**Release**|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_ubuntu)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_ubuntu/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_windows_nt)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_windows_nt/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_osx)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_osx/) |

## Prerequisites

In order to build dotnet-cli, you need the following installed on you machine

### For Windows

1. Visual Studio 2015 with Web Development Tools
  * Beta8 is available here and should work: http://www.microsoft.com/en-us/download/details.aspx?id=49442
    * Install `WebToolsExtensionsVS14.msi` and `DotNetVersionManager-x64.msi`
2. CMake (available from https://cmake.org/) is required to build the native host `corehost`

## Building/Running

1. Run `build.cmd` or `build.sh` from the root
2. Use `artifacts/{os}-{arch}/stage2/dotnet` to try out the `dotnet` command. You can also add `artifacts/{os}-{arch}/stage2` to the PATH if you want to run `dotnet` from anywhere.

## Visual Studio Code

* You can also use Visual Studo code https://code.visualstudio.com/

## A simple test

Note: The explicit `--framework` and `--runtime` switches will definitely be going away :)

1. `cd test\TestApp`
2. `dotnet publish --framework dnxcore50 --runtime win7-x64`
