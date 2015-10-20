# .NET Command Line Interface

## Building/Running

1. Run `build.cmd` or `build.sh` from the root
2. Use `artifacts/{os}-{arch}/stage2/dotnet` to try out the `dotnet` command. You can also add `artifacts/{os}-{arch}/stage2` to the PATH if you want to run `dotnet` from anywhere.

## Notes

Right now the CLI uses [DNX](https://github.com/aspnet/dnx) for NuGet restore.

## Visual Studio

* Requires VS 2015 with Web Development Tools installed to open in VS
  * Beta8 is available here and should work: http://www.microsoft.com/en-us/download/details.aspx?id=49442
    * Install `WebToolsExtensionsVS14.msi` and `DotNetVersionManager-x64.msi`
* Requires that you have a DNX installed (the build script _should_ set it up for you though)
* Compilation is not required before building, but you must run `dnu restore` (which comes from the DNX commands) after changing dependencies. If you add/remove dependencies in VS, it will run it for you

## Visual Studio Code

* You can also use Visual Studo code https://code.visualstudio.com/

## A simple test

Note: The explicit `--framework` and `--runtime` switches will definitely be going away :)

1. `cd test\TestApp`
2. `dotnet publish --framework dnxcore50 --runtime win7-x64`
