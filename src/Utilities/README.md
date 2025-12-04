# Microsoft.Build.Utilities.Core

This package contains `Microsoft.Build.Utilities.Core.dll`, which defines helper functionality for MSBuild extenders, including

* [`Task`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.task), a base class for custom tasks,
* [`ToolTask`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.tooltask), a base class for tasks that run a command-line tool, and
* [`Logger`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.logger), a base class for custom logging functionality.

### netstandard2.0 target
The `netstandard2.0` target of this build is configured only to output reference assemblies; at runtime MSBuild will be `net8.0` or `net472`. Please use the `net8.0`-targeted assemblies for .NET 8+ scenarios.

For context, see https://github.com/dotnet/msbuild/pull/6148