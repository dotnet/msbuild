# Microsoft.Build.Framework

This package contains `Microsoft.Build.Framework.dll`, which defines [fundamental types](https://docs.microsoft.com/dotnet/api/microsoft.build.framework) used in MSBuild's API and extensibility model.

The items in this namespace are primarily base-level classes and interfaces shared across MSBuild's object model.  MSBuild task or extension developers can reference this package to implement interfaces such as
[`ITask`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.itask), and [`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.ilogger).
