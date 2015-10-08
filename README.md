# .NET Command Line Interface

## Building/Running

1. Run `build.cmd` or `build.sh` from the root
2. Use `scripts/dotnet` to try out the `dotnet` command.

## Notes

Right now the CLI uses [DNX](https://github.com/aspnet/dnx) as an application host. Eventually it will become self-hosted, but for now that means a few things:

* Requires VS 2015 with Web Development Tools installed to open in VS
* Requires that you have a DNX installed (the build script _should_ set it up for you though)
* Compilation is not required before building, but you must run `dnu restore` (which comes from the DNX commands) after changing dependencies. If you add/remove dependencies in VS, it will run it for you
