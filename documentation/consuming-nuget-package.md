# Consuming MSBuild NuGet packages

The MSBuild team currently publishes five NuGet packages.  Our packages are published to NuGet.org 

| Package ID    | URL      | Status   |
| ------------- |-------------| -----|
| Microsoft.Build.Framework      | https://www.nuget.org/Packages/Microsoft.Build.Framework | [![Microsoft.Build.Framework package](https://img.shields.io/nuget/vpre/Microsoft.Build.Framework.svg)](https://www.nuget.org/Packages/Microsoft.Build.Framework) |
| Microsoft.Build.Utilities.Core      | https://www.nuget.org/Packages/Microsoft.Build.Utilities.Core | [![Microsoft.Build.Utilities.Core package](https://img.shields.io/nuget/vpre/Microsoft.Build.Utilities.Core.svg)](https://www.nuget.org/Packages/Microsoft.Build.Utilities.Core) |
| Microsoft.Build.Tasks.Core      | https://www.nuget.org/Packages/Microsoft.Build.Tasks.Core | [![Microsoft.Build.Tasks.Core package](https://img.shields.io/nuget/vpre/Microsoft.Build.Tasks.Core.svg)](https://www.nuget.org/Packages/Microsoft.Build.Tasks.Core) |
| Microsoft.Build      | https://www.nuget.org/Packages/Microsoft.Build | [![Microsoft.Build package](https://img.shields.io/nuget/vpre/Microsoft.Build.svg)](https://www.nuget.org/Packages/Microsoft.Build) |
| Microsoft.Build.Runtime      | https://www.nuget.org/Packages/Microsoft.Build.Runtime | [![Microsoft.Build.Runtime package](https://img.shields.io/nuget/vpre/Microsoft.Build.Runtime.svg)](https://www.nuget.org/Packages/Microsoft.Build.Runtime) |

## Microsoft.Build.Framework
This package contains the `Microsoft.Build.Framework.dll` assembly which makes available items in the [Microsoft.Build.Framework](https://msdn.microsoft.com/en-us/library/microsoft.build.framework.aspx) namespace.
The items in this namespace are primarily base-level classes and interfaces shared across MSBuild's object model.  MSBuild task developers can reference this package to implement interfaces such as
[ITask](https://msdn.microsoft.com/en-us/library/microsoft.build.framework.itask.aspx), [ILogger](https://msdn.microsoft.com/en-us/library/microsoft.build.framework.ilogger.aspx), and
[IForwardingLogger](https://msdn.microsoft.com/en-us/library/microsoft.build.framework.iforwardinglogger.aspx).

## Microsoft.Build.Utilities.Core
This package contains the `Microsoft.Build.Utilities.Core.dll` assembly which makes available items in the [Microsoft.Build.Utilities](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities.aspx) namespace.
The items in this namespace are used by MSBuild to implement utility classes which do things such as create command lines, implement ILogger, locate tools, and track dependencies.

MSBuild task developers often reference this package to develop tasks that inherit from the base class [Task](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities.task.aspx).  This class is implements [ITask] 
but also provides a logging helper which can reduce code required to develop an MSBuild task.  It also contains the [ToolTask](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities.tooltask.aspx) class which
should be used by tasks which wrap the execution of another tool.  It provides functionality to capture standard output and standard error as well as the exit code of the process.

## Microsoft.Build.Tasks.Core
This package contains the `Microsoft.Build.Tasks.Core.dll` assembly which makes available items in the [Microsoft.Build.Tasks](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks.aspx) namespace.
The items in this namespace are MSBuild tasks that have been developed by the MSBuild team.  This includes [Copy](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks.copy.aspx),
[Csc](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks.csc.aspx), and [Exec](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks.exec.aspx).

Most developers do not need to reference this package unless they want to extend a stock MSBuild task with custom functionality.  Alternatively, we recommend that MSBuild task developers reference the 
`Microsoft.Build.Utilites.Core` package and implement the abstract class [Task](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities.task.aspx) or
[ToolTask](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities.tooltask.aspx).

## Microsoft.Build
This package contains the `Microsoft.Build.dll` assembly which makes available items in the [Microsoft.Build.Construction](https://msdn.microsoft.com/en-us/library/microsoft.build.construction.aspx),
[Microsoft.Build.Evaluation](https://msdn.microsoft.com/en-us/library/microsoft.build.evaluation.aspx), and [Microsoft.Build.Execution](https://msdn.microsoft.com/en-us/library/microsoft.build.execution.aspx) namespaces.
Developers should reference this package to create, edit, evaluate, or build MSBuild projects.

To create or edit an MSBuild project, use the [Microsoft.Build.Construction.ProjectRootElement](https://msdn.microsoft.com/en-us/library/microsoft.build.construction.projectrootelement.aspx) class and call the 
[Create](https://msdn.microsoft.com/en-us/library/microsoft.build.construction.projectrootelement.create.aspx) or
[Open](https://msdn.microsoft.com/en-us/library/microsoft.build.construction.projectrootelement.open.aspx) method.

To evaluate or build an MSBuild project, use the [Microsoft.Build.Evaluation.Project](https://msdn.microsoft.com/en-us/library/microsoft.build.evaluation.project.aspx) class by creating an instance of it with the
appropriate parameters for your project.  To retrieve evaluated items, call methods such as  properties such as [GetItem](https://msdn.microsoft.com/en-us/library/microsoft.build.evaluation.project.getitems.aspx)
or [GetPropertyValue](https://msdn.microsoft.com/en-us/library/microsoft.build.evaluation.project.getpropertyvalue.aspx).

## Microsoft.Build.Runtime
This package contains the standard set of MSBuild projects which are imported by other projects such as CSharp and Visual Basic as well as the MSBuild executable.  Developers should reference this package if they want to
redistribute the MSBuild runtime to evaluate or build MSBuild projects within their application.  This can be necessary because prior to MSBuild version 15, MSBuild was installed globally on a machine and universally
available to all applications.  However, in MSBuild version 15 and forward, MSBuild is redistributed by each application that uses it and applications are unable to share other instances.  
