### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSBuildTask0001 | MSBuild.TaskAuthoring | Error | APIs that must not be used in any MSBuild task (Environment.Exit, Console.*, etc.)
MSBuildTask0002 | MSBuild.TaskAuthoring | Warning, Info | Warning for IMultiThreadableTask, Info for plain ITask. APIs that should use TaskEnvironment alternatives.
MSBuildTask0003 | MSBuild.TaskAuthoring | Warning, Info | Warning for IMultiThreadableTask, Info for plain ITask. File APIs that need absolute paths.
MSBuildTask0004 | MSBuild.TaskAuthoring | Warning, Info | Warning for IMultiThreadableTask, Info for plain ITask. APIs that may cause issues in multithreaded task execution.
MSBuildTask0005 | MSBuild.TaskAuthoring | Warning, Info | Warning for IMultiThreadableTask, Info for plain ITask. Transitive unsafe API usage detected in task call chain.
