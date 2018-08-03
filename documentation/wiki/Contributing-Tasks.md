# Contributing Tasks

MSBuild tasks are units of executable code used to perform atomic build operations.  There are many tasks already in MSBuild but there is always a need for more.  We encourage you to contribute useful tasks directory to MSBuild.

## Getting Started
Please [open an issue](https://github.com/Microsoft/msbuild/issues/new) to propose a new task.  This gives the community a chance to provide feedback and make suggestions.  Once there is consensus that the task is needed and the below requirements are met, fork the repository and begin development.

## Requirements
The following requirements are in place for contributed tasks:

1. The task must not introduce the need to ship any third-party assemblies.
2. The task should work on .NET Framework and .NET Core if possible.  It can be confusing to users if a task only works on certain platforms.
3. The task must have unit tests in place to prevent regressions.

## Developing a new Task
Review the existing documentation on [Task Writing](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-writing) to learn about the fundamentals.  You can also looking at existing tasks in the [Microsoft.Build.Tasks.Core assembly](https://github.com/Microsoft/msbuild/tree/master/src/Tasks) for a great starting point.

Tasks are generally simple and should not require much effort to develop.  If you find a task becoming very complicated, consider breaking it up into smaller tasks which can be run together in a target.

## Developing unit tests
Contributed tasks must have unit tests in place to prove they work and to prevent regressions caused by other code changes.  There are a lot of examples in the [Microsoft.Build.Tasks.UnitTests](https://github.com/Microsoft/msbuild/tree/master/src/Tasks.UnitTests) project.  Please provide a reasonable amount of test coverage so ensure the quality of the product.

## Documentation
You can document the new task in the [visualstudio-docs](https://github.com/MicrosoftDocs/visualstudio-docs/tree/master/docs/msbuild) repository.  This helps users discover the new functionality.  The easiest way is to copy the documentation page for an existing task as a template.

## Ship schedule
MSBuild ships regularly with Visual Studio.  It also is updated in Preview releases.  Once your contribution is merged, expect it to be available in the next release.


