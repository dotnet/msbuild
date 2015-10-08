# .NET Command Line Interface

## Building/Running

1. Run `build.cmd` or `build.sh` from the root
2. Use `scripts/dotnet` to try out the `dotnet` command.

## Notes

Right now the CLI uses [DNX](https://github.com/aspnet/dnx) as an application host. Eventually it will become self-hosted, but for now that means a few things:

* Requires VS 2015 with Web Development Tools installed to open in VS
  * Beta7 is available here and should work: https://www.microsoft.com/en-us/download/details.aspx?id=48738&fa43d42b-25b5-4a42-fe9b-1634f450f5ee=True
    * Install `WebToolsExtensionsVS14.msi` and `DotNetVersionManager-x64.msi`
  * There are also more recent builds available here: `\\vwdbuild01\Drops\WTE\Release.Nightly\Dev14\Latest-Successful\Release\Signed\MSI` (run `InstallWTE.cmd`)
* Requires that you have a DNX installed (the build script _should_ set it up for you though)
* Compilation is not required before building, but you must run `dnu restore` (which comes from the DNX commands) after changing dependencies. If you add/remove dependencies in VS, it will run it for you

## A simple test (windows only for now)

Note: The explicit `--framework` and `--runtime` switches will definitely be going away :)

1. `cd test\TestApp`
2. `..\..\scripts\dotnet run --framework dnxcore50 --runtime win7-x86`
