### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSBuildTask0001 | MSBuild.TaskAuthoring | Error | APIs that must not be used in any MSBuild task (Environment.Exit, Console.*, etc.)
MSBuildTask0002 | MSBuild.TaskAuthoring | Warning | APIs that should use TaskEnvironment alternatives
MSBuildTask0003 | MSBuild.TaskAuthoring | Warning | File APIs that need absolute paths
MSBuildTask0004 | MSBuild.TaskAuthoring | Warning | APIs that may cause issues in multithreaded task execution
MSBuildTask0005 | MSBuild.TaskAuthoring | Warning | Transitive unsafe API usage detected in task call chain
MSBuildTask0006 | MSBuild.TaskAuthoring | Info | Prefer typed path parameter (AbsolutePath/FileInfo/DirectoryInfo) over string (code fix available)
MSBuildTask0007 | MSBuild.TaskAuthoring | Info | Prefer ITaskItem<T> over manual ItemSpec parsing (code fix available)
MSBuildTask0008 | MSBuild.TaskAuthoring | Info | Initialize a relative default path in Execute() so TaskEnvironment can root it when the property is retyped (code fix available)
MSBuildTask0009 | MSBuild.TaskAuthoring | Warning | ITaskItem<T> used with a type argument T that MSBuild cannot bind as a task parameter
