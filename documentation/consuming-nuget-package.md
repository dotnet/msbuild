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

## Target framework support and reference-only assets

The MSBuild packages are the build engine that ships **inside** the .NET SDK and
Visual Studio. They are not general-purpose libraries meant to be redistributed
and run on an arbitrary runtime. This shapes how the packages are laid out and
which target frameworks they "support."

### One .NET runtime per band, plus .NET Framework

Each MSBuild release band ships a runtime (`lib/`) assembly for exactly one .NET
(Core) target framework — the one the matching SDK runs on — plus `net472` for
Visual Studio. For example:

| MSBuild version | .NET runtime asset | .NET Framework runtime asset |
| --------------- | ------------------ | ---------------------------- |
| 17.11           | `lib/net8.0`       | `lib/net472`                 |
| 17.14           | `lib/net9.0`       | `lib/net472`                 |
| 18.x            | `lib/net10.0`      | `lib/net472`                 |

A newer band advancing its .NET target framework (for example `net9.0` →
`net10.0`) is expected, not a regression. If you need to *run* against a specific
.NET runtime, choose the MSBuild band whose SDK ships that runtime.

### The `netstandard2.0` asset is compile-only (`ref/`, no `lib/`)

The packages also include a `netstandard2.0` **reference assembly** under
`ref/netstandard2.0`, but no `lib/netstandard2.0` runtime assembly. This is
intentional: it is a compile-time surface, not a redistributable runtime.

It exists so that a single MSBuild extension — a task, logger, analyzer, or SDK
resolver — can be compiled once (typically as `netstandard2.0`) and then loaded
into either .NET Framework MSBuild or .NET MSBuild, with the **host** supplying
the matching runtime implementation. Binding to the host's copy guarantees your
extension talks to the exact engine that is running the build, rather than a
mismatched copy shipped alongside it.

Because NuGet computes compile compatibility from the `ref/` folder, a project
targeting a framework compatible with `netstandard2.0` (for example `net8.0`)
will **compile** successfully against these packages even when there is no
matching runtime asset. At run time the assembly will not be found unless an
MSBuild host provides it, and the packages emit a build warning to that effect.

If you are authoring an MSBuild extension that runs inside an MSBuild host,
reference the package with compile-only assets:

```xml
<PackageReference Include="Microsoft.Build.Utilities.Core" Version="..."
                  PrivateAssets="all" ExcludeAssets="runtime" />
```

If your code instead runs standalone (outside an MSBuild host), target a
framework for which the package ships a runtime assembly (see the table above).
Do not simply suppress the warning — suppressing it only converts a build
failure into a load-time failure.
