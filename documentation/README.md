
# Start developing on .NET Command Line Interface
## Prerequisites

In order to build .NET Command Line Interface, you need the following installed on you machine.

### For Windows

1. Visual Studio 2015 with Web Development Tools
  * Beta8 is available here and should work: http://www.microsoft.com/en-us/download/details.aspx?id=49442
    * Install `WebToolsExtensionsVS14.msi` and `DotNetVersionManager-x64.msi`
2. CMake (available from https://cmake.org/) is required to build the native host `corehost`. Make sure to add it to the PATH.

### For Linux

You need CMake in your path. 

### For OS X

[TODO]

## Building/Running

1. Run `build.cmd` or `build.sh` from the root depending on your OS.
2. Use `artifacts/{os}-{arch}/stage2/dotnet` to try out the `dotnet` command. You can also add `artifacts/{os}-{arch}/stage2` to the PATH if you want to run `dotnet` from anywhere.


# Tools 

## Visual Studio

* You can use Visual Studio 2015 to work on these bits. 

## Visual Studio Code

* You can also use Visual Studo code https://code.visualstudio.com/ to contribute to this project. 

## A simple test

1. `cd test\TestApp`
2. `dotnet run`

## Contributing to the repo

Once you are set up with requirements and you want to start, please review our [contribution guidelines](Contributing.md) to get up to speed with the process. 


# I just want to use this toolchain

If you just want to use the .NET Command Line Interface, your best bet would be to use the installers provided on the [main README file](../README.md). You can also follow the above guide for building from source to get the lastest (bleeding edge) bits. 
