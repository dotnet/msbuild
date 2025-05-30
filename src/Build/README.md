# Microsoft.Build

This package contains `Microsoft.Build.dll`, which defines MSBuild's API, including

* [`Microsoft.Build.Evaluation`](https://docs.microsoft.com/dotnet/api/microsoft.build.evaluation) for evaluating MSBuild projects,
* [`Microsoft.Build.Construction`](https://docs.microsoft.com/dotnet/api/microsoft.build.construction) for creating new MSBuild projects, and
* [`Microsoft.Build.Execution`](https://docs.microsoft.com/dotnet/api/microsoft.build.execution) for building MSBuild projects.

Developers should reference this package to write applications that create, edit, evaluate, or build MSBuild projects.

To create or edit an MSBuild project, use the [Microsoft.Build.Construction.ProjectRootElement](https://docs.microsoft.com/dotnet/api/microsoft.build.construction.projectrootelement) class and call the
[Create](https://docs.microsoft.com/dotnet/api/microsoft.build.construction.projectrootelement.create) or
[Open](https://docs.microsoft.com/dotnet/api/microsoft.build.construction.projectrootelement.open) method.

To evaluate or build an MSBuild project, use the [Microsoft.Build.Evaluation.Project](https://docs.microsoft.com/dotnet/api/microsoft.build.evaluation.project) class by creating an instance of it with the
appropriate parameters for your project.  To retrieve evaluated items, call methods such as  properties such as [GetItems](https://docs.microsoft.com/dotnet/api/microsoft.build.evaluation.project.getitems)
or [GetPropertyValue](https://docs.microsoft.com/dotnet/api/microsoft.build.evaluation.project.getpropertyvalue).
