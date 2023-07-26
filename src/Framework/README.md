# Microsoft.Build.Framework

This package contains `Microsoft.Build.Framework.dll`, which defines [fundamental types](https://docs.microsoft.com/dotnet/api/microsoft.build.framework) used in MSBuild's API and extensibility model.

The items in this namespace are primarily base-level classes and interfaces shared across MSBuild's object model.  MSBuild task or extension developers can reference this package to implement interfaces such as
[`ITask`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.itask), and [`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.ilogger).

### netstandard2.0 target
The `netstandard2.0` target of this build is configured only to output reference assemblies; at runtime MSBuild will be `net8.0` or `net472`. Please use the `net8.0`-targeted assemblies for .NET 8+ scenarios.

For context, see https://github.com/dotnet/msbuild/pull/6148