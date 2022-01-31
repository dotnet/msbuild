# Consuming MSBuild NuGet packages

The MSBuild team currently publishes five NuGet packages.  Our packages are published to NuGet.org

| Package ID    | URL      | Latest Version   |
| ------------- |-------------| -----|
| Microsoft.Build.Framework      | https://www.nuget.org/Packages/Microsoft.Build.Framework | [![Microsoft.Build.Framework package](https://img.shields.io/nuget/vpre/Microsoft.Build.Framework.svg)](https://www.nuget.org/Packages/Microsoft.Build.Framework) |
| Microsoft.Build.Utilities.Core      | https://www.nuget.org/Packages/Microsoft.Build.Utilities.Core | [![Microsoft.Build.Utilities.Core package](https://img.shields.io/nuget/vpre/Microsoft.Build.Utilities.Core.svg)](https://www.nuget.org/Packages/Microsoft.Build.Utilities.Core) |
| Microsoft.Build.Tasks.Core      | https://www.nuget.org/Packages/Microsoft.Build.Tasks.Core | [![Microsoft.Build.Tasks.Core package](https://img.shields.io/nuget/vpre/Microsoft.Build.Tasks.Core.svg)](https://www.nuget.org/Packages/Microsoft.Build.Tasks.Core) |
| Microsoft.Build      | https://www.nuget.org/Packages/Microsoft.Build | [![Microsoft.Build package](https://img.shields.io/nuget/vpre/Microsoft.Build.svg)](https://www.nuget.org/Packages/Microsoft.Build) |
| Microsoft.Build.Runtime      | https://www.nuget.org/Packages/Microsoft.Build.Runtime | [![Microsoft.Build.Runtime package](https://img.shields.io/nuget/vpre/Microsoft.Build.Runtime.svg)](https://www.nuget.org/Packages/Microsoft.Build.Runtime) |

## Microsoft.Build.Framework

This package contains `Microsoft.Build.Framework.dll`, which defines [fundamental types](https://docs.microsoft.com/dotnet/api/microsoft.build.framework) used in MSBuild's API and extensibility model.

## Microsoft.Build.Utilities.Core

This package contains the `Microsoft.Build.Utilities.Core.dll` assembly which makes available items in the [Microsoft.Build.Utilities](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities) namespace.

## Microsoft.Build.Tasks.Core

This package contains implementations of [commonly-used MSBuild
tasks](https://docs.microsoft.com/visualstudio/msbuild/msbuild-task-reference)
that ship with MSBuild itself.

Most developers do not need to reference this package. We recommend that MSBuild
task developers reference the `Microsoft.Build.Utilities.Core` package and
implement the abstract class
[`Task`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.task)
or
[`ToolTask`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.tooltask).

## Microsoft.Build

This package contains the `Microsoft.Build.dll` assembly which makes available items in the [Microsoft.Build.Construction](https://msdn.microsoft.com/library/microsoft.build.construction.aspx),
[Microsoft.Build.Evaluation](https://msdn.microsoft.com/library/microsoft.build.evaluation.aspx), and [Microsoft.Build.Execution](https://msdn.microsoft.com/library/microsoft.build.execution.aspx) namespaces.

Developers should reference this package to create, edit, evaluate, or build MSBuild projects.

## Microsoft.Build.Runtime

This package delivers a complete executable copy of MSBuild. Reference this
package only if your application needs to load projects or execute in-process
builds without requiring installation of MSBuild. Successfully evaluating
projects using this package requires aggregating additional components (like the
compilers) into an application directory.

🗒️ NOTE: if you are building an application that wants to use MSBuild to
evaluate or build projects, you will generally not need this package. Instead,
use [MSBuildLocator](https://aka.ms/msbuild/locator) to use a complete toolset
provided by the .NET SDK or Visual Studio.
