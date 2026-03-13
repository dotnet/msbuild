### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSBuildTask0001 | MSBuild.TaskAuthoring | Error | APIs that must not be used in any MSBuild task (Environment.Exit, Console.*, etc.)
MSBuildTask0002 | MSBuild.TaskAuthoring | Warning | APIs that should use TaskEnvironment alternatives in IMultiThreadableTask
MSBuildTask0003 | MSBuild.TaskAuthoring | Warning | File APIs that need absolute paths in IMultiThreadableTask
MSBuildTask0004 | MSBuild.TaskAuthoring | Warning | APIs that may cause issues in multithreaded task execution
MSBuildTask0005 | MSBuild.TaskAuthoring | Warning | Transitive unsafe API usage detected in task call chain
