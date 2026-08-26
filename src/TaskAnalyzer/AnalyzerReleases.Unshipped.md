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
MSBuildTask0010 | MSBuild.TaskAuthoring | Warning | ITaskItem<T> used with a type argument T that MSBuild parses through Convert.ChangeType
MSBuildTask0011 | MSBuild.TaskAuthoring | Info | Prefer constructor injection for TaskEnvironment
MSBuildTask0012 | MSBuild.TaskAuthoring | Warning | TaskEnvironment property is never assigned by MSBuild because the task does not implement IMultiThreadableTask
MSBuildTask0013 | MSBuild.TaskAuthoring | Info | Task declares IMultiThreadableTask but is not marked with [MSBuildMultiThreadableTask] (disabled by default)
MSBuildTask0014 | MSBuild.TaskAuthoring | Warning | [MSBuildMultiThreadableTask] applied to a type MSBuild never routes as a task -- not an ITask, or an abstract task whose attribute no subclass inherits -- where it has no effect
MSBuildTask0015 | MSBuild.TaskAuthoring | Warning | Concrete task type does not declare multithreading support; reports only when opted into with `msbuild_task_analyzer.scope = require_multithreadable` or an explicit severity (code fix available)
