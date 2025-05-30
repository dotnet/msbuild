# Microsoft.Build.Tasks

This package contains implementations of [commonly-used MSBuild
tasks](https://docs.microsoft.com/visualstudio/msbuild/msbuild-task-reference)
that ship with MSBuild itself.

You do not need to reference this package to use these tasks in a build--they
are available in any MSBuild environment.

If you are writing a new task, you may wish to reference
[Microsoft.Build.Utilities.Core](https://www.nuget.org/Packages/Microsoft.Build.Utilities.Core)
and derive from `Microsoft.Build.Utilities.Task` or
`Microsoft.Build.Utilities.ToolTask`.
