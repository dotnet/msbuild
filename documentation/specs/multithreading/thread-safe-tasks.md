# Thread-Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization. With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to ensure that multiple tasks do not access/modify shared process state, and the relative paths are resolved correctly.

To enable this multithreaded execution model, tasks will declare their capability to run in multiple threads within one process. These capabilities are referred to as **thread-safety** capabilities and the corresponding tasks are called **thread-safe tasks**. Thread-safe tasks must avoid using APIs that modify or depend on global process state, as this could cause conflicts when multiple tasks execute concurrently. See [Thread-Safe Tasks API Analysis Reference](thread-safe-tasks-api-analysis.md) for detailed guidelines. Task authors also get a `TaskEnvironment` that provides safe alternatives to global process state APIs. Use `TaskEnvironment.GetAbsolutePath()` to root relative paths.

Tasks that are not thread-safe can still participate in multithreaded builds. MSBuild will execute these tasks in separate TaskHost processes to provide process-level isolation.

## TaskAnalyzer

The `Microsoft.Build.Framework` package delivers TaskAnalyzer to C# task projects. TaskAnalyzer checks the MT contract during compilation.

By default, MT-specific diagnostics apply only after a task declares MT support. This default prevents new diagnostics in regular task projects.

Set `MSBuildTaskAnalyzerScope` to `all` to inspect regular tasks before migration. Set `MSBuildTaskAnalyzerEnabled` to `false` to disable TaskAnalyzer.

For installation, configuration, rule actions, and migration examples, see the [TaskAnalyzer guide](../../../src/TaskAnalyzer/README.md).

## Thread-Safe Capability Indicators

Task authors use two thread-safe indicators:
1. **`IMultiThreadableTask`** provides access to safe APIs through `TaskEnvironment`.
2. **`MSBuildMultiThreadableTask`** permits in-process execution in MT mode.

Apply `[MSBuildMultiThreadableTask]` directly to each task that can safely run in-process. Implement `IMultiThreadableTask` when the task also requires `TaskEnvironment`.

Tasks that use `TaskEnvironment` cannot load in older MSBuild versions that do not support multithreading features, requiring authors to drop support for older MSBuild versions. To address this challenge, MSBuild provides a compatibility bridge that allows certain tasks targeting older MSBuild versions to participate in multithreaded builds. While correct absolute path resolution can be and should be achieved without accessing `TaskEnvironment` in tasks that use compatibility bridge options, tasks must avoid relying on environment variables or modifying global process state.

So, task authors who need to support older MSBuild versions will have three choices:
1. **Maintain separate implementations** - Create and support both thread-safe and legacy versions of the same task.
2. **Use compatibility bridge approaches** - Rely on MSBuild's ability to run legacy tasks in multithreaded mode without access to `TaskEnvironment`.
3. **Accept reduced performance** - Tasks will execute more slowly than their thread-safe versions because they must run in a separate TaskHost process

### TaskEnvironment Access

Tasks get `TaskEnvironment` by implementing the `IMultiThreadableTask` interface.

```csharp
namespace Microsoft.Build.Framework;
public interface IMultiThreadableTask : ITask
{
    TaskEnvironment TaskEnvironment { get; set; }
}
```

Built-in MSBuild tasks initialize `TaskEnvironment` with a `MultiProcessTaskEnvironmentDriver`-backed default. This ensures tasks have a usable `TaskEnvironment` even when explicitly instantiated outside the engine (e.g., `new Copy()`) or run in the out-of-proc task host. The engine's in-proc path (`TaskExecutionHost.InitializeForBatch`) overwrites the default with the appropriate driver before `Execute()` is called.

#### Constructor Injection of `TaskEnvironment`

When the engine instantiates a task type, it first looks for a public constructor that accepts a single `TaskEnvironment` parameter. If one exists, the engine calls it with the current `TaskEnvironment` instead of the parameterless constructor; otherwise it falls back to the parameterless constructor as before. The engine still assigns the `IMultiThreadableTask.TaskEnvironment` property after construction, so tasks that use the constructor should assign the property from within it.

This exists because C# property initializers run during object construction (before the constructor body completes)—before the engine can assign the `TaskEnvironment` property—so an initializer cannot rely on the environment. Constructor injection lets a task compute environment-dependent default values (for example, rooting a default output directory) during construction:

```csharp
[MSBuildMultiThreadableTask]
public sealed class MyTask : Task, IMultiThreadableTask
{
    public MyTask(TaskEnvironment taskEnvironment)
    {
        TaskEnvironment = taskEnvironment;

        // Now a reasonable default can be computed using the environment.
        IntermediateOutputDir = new DirectoryInfo(TaskEnvironment.GetAbsolutePath("obj").Value);
    }

    public TaskEnvironment TaskEnvironment { get; set; }

    public DirectoryInfo IntermediateOutputDir { get; set; }
}
```

In multi-process execution (out-of-proc task host) and when a task is instantiated outside the engine, `TaskEnvironment.Fallback` is supplied to the constructor. A task that declares only a `TaskEnvironment` constructor (no parameterless constructor) is therefore still instantiable everywhere the engine runs it. This should not be combined with `LoadInSeparateAppDomain`, because `TaskEnvironment` is not marshalable across AppDomain boundaries.

Constructor injection also applies to host-registered tasks (the reflection-free `Microsoft.Build.Utilities.Task.RegisterTask` path used for trimming/Native AOT). The generic `RegisterTask<T>(string)` overload injects the `TaskEnvironment` when `T` declares such a constructor (its public constructors are already trim-rooted). Because that overload's `new()` constraint requires a parameterless constructor, a task whose *only* constructor takes a `TaskEnvironment` is registered through the `RegisterTask(string, Func<TaskEnvironment, ITask>)` factory overload, which hands the environment to the factory. See [task-class-registration-api.md](../task-class-registration-api.md).

The task-authoring analyzer reports `MSBuildTask0011` at Info severity for concrete `IMultiThreadableTask` implementations that rely only on post-construction property injection. Add a public constructor whose single parameter is `TaskEnvironment` to make the environment available during construction. A task may retain a public parameterless constructor for callers that instantiate it directly; the engine prefers the injecting constructor when both are present.

Task authors who want to support older MSBuild versions need to:
- Maintain both thread-safe and legacy implementations.
- Use conditional task declarations based on MSBuild version to select which assembly to load the task from.

**Note:** Consider backporting `IMultiThreadableTask` to MSBuild 17.14 for graceful failure when the interface is used.

### In-Process MT Execution

Apply the attribute directly to each task class that can run in-process. The attribute does not provide access to `TaskEnvironment`.

```csharp
namespace Microsoft.Build.Framework;
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MSBuildMultiThreadableTaskAttribute : Attribute
{
    public MSBuildMultiThreadableTaskAttribute() { }
}
```

MSBuild detects `MSBuildMultiThreadableTaskAttribute` by its namespace and name only. This permits a compatibility attribute in task assemblies that target older Framework packages.

New task projects can use the attribute from `Microsoft.Build.Framework`. The attribute is not inherited.

For tasks to be eligible for multithreaded execution using this approach, they must satisfy the following conditions:
- The task must not modify global process state (environment variables, working directory)
- The task must not depend on global process state, including relative path resolution

#### API Usage Example

```csharp
[MSBuildMultiThreadableTask]
public class MyTask : Task {...}
```

## TaskEnvironment API

The `TaskEnvironment` provides thread-safe alternatives to APIs that use global process state, enabling tasks to execute safely in a multithreaded environment.

```csharp
namespace Microsoft.Build.Framework;
public interface IMultiThreadableTask : ITask
{
    TaskEnvironment TaskEnvironment { get; set; }
}

public class TaskEnvironment
{ 
    public AbsolutePath ProjectDirectory { get; internal set; }

    // This function resolves paths relative to ProjectDirectory.
    public AbsolutePath GetAbsolutePath(string path);
    
    public string? GetEnvironmentVariable(string name);
    public IReadOnlyDictionary<string, string> GetEnvironmentVariables();
    public void SetEnvironmentVariable(string name, string? value);

    public ProcessStartInfo GetProcessStartInfo();
}
```

The `TaskEnvironment` class that MSBuild provides is not thread-safe. Task authors who spawn multiple threads within their task implementation must provide their own synchronization when accessing the task environment from multiple threads. However, each task receives its own isolated environment object, so synchronization with other concurrent tasks is not required.

### Path Handling

To prevent common thread-safety issues related to path handling, we introduce path type that is implicitly convertible to string:

```csharp
namespace Microsoft.Build.Framework;
public readonly struct AbsolutePath : IEquatable<AbsolutePath>
{
    // Default value returns string.Empty for Path property
    public string Value { get; }
    internal AbsolutePath(string path, bool ignoreRootedCheck) { }
    public AbsolutePath(string path); // Checks Path.IsPathRooted
    public AbsolutePath(string path, AbsolutePath basePath) { }
    public static implicit operator string(AbsolutePath path) { }
    public override string ToString() => Value;

    // overrides for equality and hashcode
}
```

`AbsolutePath` converts implicitly to string for seamless integration with existing File/Directory APIs.

### API Usage Example

```csharp
public bool Execute(...)
{
    // Use APIs provided by TaskEnvironment
    string envVar = TaskEnvironment.GetEnvironmentVariable("EnvVar");
       
    // Convert string properties to strongly-typed paths and use them in standard File/Directory APIs
    AbsolutePath path = TaskEnvironment.GetAbsolutePath("SomePath");
    string content = File.ReadAllText(path);
    string content2 = File.ReadAllText(path.ToString());
    string content3 = File.ReadAllText(path.Value);
    ...
}
```

### Temporary paths

`Path.GetTempPath()` returns a temporary directory. `Path.GetTempFileName()` creates a unique, empty file.

Reading `TMP` does not reproduce either method on all platforms. It also does not create a unique file.

TaskEnvironment does not currently provide equivalent APIs. Pass a temporary directory to an MT task as an `AbsolutePath` input when possible.

## Appendix: Alternatives

This appendix collects alternative approaches considered during design.

### Alternative Approach: API Hooking

An alternative approach to the `TaskEnvironment` API could be to use API hooking (such as Microsoft Detours) to automatically virtualize global process state without requiring any changes from task authors.

The main advantages of API hooking include requiring no action from task authors since existing tasks would work without modification or recompilation, and having no compatibility concerns with older MSBuild versions. However, it would be a Windows-only solution, making it unsuitable for cross-platform scenarios. 

### Alternative to Attribute-Based Thread-Safe Capability Declaration

We considered making the thread-safety signal using the task declaration (for example, a `ThreadSafe="true"` attribute on `UsingTask`) so that project authors could declare compatibility without changing task assemblies. However, because older MSBuild versions treat unknown attributes in task declarations as errors, this approach would require updating older MSBuild versions or servicing them to ignore the attribute. 