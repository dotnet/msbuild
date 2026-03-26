# Thread-Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization. With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to ensure that multiple tasks do not access/modify shared process state, and the relative paths are resolved correctly.

To enable this multithreaded execution model, tasks will declare their capability to run in multiple threads within one process. These capabilities are referred to as **thread-safety** capabilities and the corresponding tasks are called **thread-safe tasks**. Thread-safe tasks must avoid using APIs that modify or depend on global process state, as this could cause conflicts when multiple tasks execute concurrently. See [Thread-Safe Tasks API Analysis Reference](thread-safe-tasks-api-analysis.md) for detailed guidelines. Task authors will also get access to a `TaskEnvironment` that provides safe alternatives to global process state APIs. For example, task authors should use `TaskEnvironment.GetAbsolutePath()` instead of `Path.GetFullPath()` to ensure correct path resolution in multithreaded scenarios.

Tasks that are not thread-safe can still participate in multithreaded builds. MSBuild will execute these tasks in separate TaskHost processes to provide process-level isolation.

## Thread-Safe Capability Indicators

Task authors can declare thread-safe capabilities in two different ways:
1. **Interface-Based Thread-Safe Capability Declaration** - Provides access to thread-safe APIs through `TaskEnvironment` to be used in the task code.
2. **Attribute-Based Thread-Safe Capability Declaration** - Allows existing tasks to declare its ability run in multithreaded mode without code changes. It is a **compatibility bridge option**.

Tasks that use `TaskEnvironment` cannot load in older MSBuild versions that do not support multithreading features, requiring authors to drop support for older MSBuild versions. To address this challenge, MSBuild provides a compatibility bridge that allows certain tasks targeting older MSBuild versions to participate in multithreaded builds. While correct absolute path resolution can be and should be achieved without accessing `TaskEnvironment` in tasks that use compatibility bridge options, tasks must avoid relying on environment variables or modifying global process state.

So, task authors who need to support older MSBuild versions will have three choices:
1. **Maintain separate implementations** - Create and support both thread-safe and legacy versions of the same task.
2. **Use compatibility bridge approaches** - Rely on MSBuild's ability to run legacy tasks in multithreaded mode without access to `TaskEnvironment`.
3. **Accept reduced performance** - Tasks will execute more slowly than their thread-safe versions because they must run in a separate TaskHost process

### Interface-Based Thread-Safe Capability Declaration

Tasks indicate thread-safety capabilities by implementing the `IMultiThreadableTask` interface.

```csharp
namespace Microsoft.Build.Framework;
public interface IMultiThreadableTask : ITask
{
    TaskEnvironment TaskEnvironment { get; set; }
}
```

Similar to how MSBuild provides the abstract `Task` class with default implementations for the `ITask` interface, MSBuild will offer a `MultiThreadableTask` abstract class with default implementations for the `IMultiThreadableTask` interface. Task authors will only need to implement the `Execute` method for the `ITask` interface and use `TaskEnvironment` within it to create their thread-safe tasks.

```csharp
namespace Microsoft.Build.Utilities;
public abstract class MultiThreadableTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment{ get; set; }
}
```

Task authors who want to support older MSBuild versions need to:
- Maintain both thread-safe and legacy implementations.
- Use conditional task declarations based on MSBuild version to select which assembly to load the task from.

**Note:** Consider backporting `IMultiThreadableTask` to MSBuild 17.14 for graceful failure when the interface is used.

### Attribute-Based Thread-Safe Capability Declaration

Task authors can indicate thread-safety capabilities by marking their task classes with a specific attribute. Tasks marked with this attribute can run in multithreaded builds but do not have access to `TaskEnvironment` APIs.

```csharp
namespace Microsoft.Build.Framework;
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal class MSBuildMultiThreadableTaskAttribute : Attribute
{
    public MSBuildMultiThreadableTaskAttribute() { }
}
```

MSBuild detects `MSBuildMultiThreadableTaskAttribute` by its namespace and name only, ignoring the defining assembly, which allows customers to define the attribute in their own assemblies alongside their tasks. Since MSBuild does not ship the attribute, customers using newer MSBuild versions should prefer the Interface-Based Thread-Safe Capability Declaration.

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
    string content3 = File.ReadAllText(path.Path);
    ...
}
```

## Managing Static State in Tasks 
Static state in tasks can cause two issues:
- **Concurrency**: Race conditions when multiple threads access shared static data
- **Lifetime**: Static data persisting unexpectedly across multiple builds

### Concurrency

In multithreaded builds, concurrent tasks sharing the same static field can cause race conditions unless the field is designed for concurrent access. Thread-safety of static fields is the task author's responsibility, same as in any multithreaded application.

### Lifetime

Static fields persist across multiple builds, meaning data cached during one build remains available in subsequent builds on the same node, regardless of changes to project state. 

By default, MSBuild reuses worker nodes between builds. Previously, tasks running on the main (scheduler) node avoided this issue because the main process terminated after each build. However, with MSBuild Server (enabled by default in multithreaded builds), the main node now also persists across builds, extending this behavior to all tasks.

This persistence is not inherently problematic and is often intentional — for example, caching expensive computations to improve performance across projects and builds. Such caching is acceptable when task authors implement proper cache invalidation strategies. The concerns below apply specifically when cached data becomes stale or incorrect and needs clean up after each build.

MSBuild provides `IBuildEngine4.RegisterTaskObject` to address the lifetime issue: an API that lets tasks store objects with explicit, engine-managed lifetimes instead of relying on static fields.

```csharp
void IBuildEngine4.RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection);
object IBuildEngine4.GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime);
```

The engine stores registered objects. Both methods are thread-safe and may be called concurrently from multiple tasks. If multiple tasks attempt to register an object with the same key concurrently, only the first registration takes effect — subsequent calls are ignored. When an object's lifetime expires, MSBuild removes it from the registry so no new consumers can retrieve it, then calls `IDisposable.Dispose` on it if it implements `IDisposable`.

`RegisteredTaskObjectLifetime` controls when objects are disposed:

| Lifetime | Disposed When | Use Case |
|----------|---------------|----------|
| `Build` | The whole build invocation completes (not a single project) | Per-build caches and resources that must not leak across builds. |
| `AppDomain` | The MSBuild process exits | Objects that are safe to share across builds. |

`Build` lifetime objects are disposed between each build request, so task authors who depended on the isolation they previously got from the entrypoint process lifetime likely prefer it.


#### Example: Migrating a Static Cache

**Before — static cache that leaks across builds:**

```csharp
public class MyTask : Task
{
    private static readonly Dictionary<string, string> s_cache = new();

    public override bool Execute()
    {
        ...
        s_cache[key] = value;
        ...
    }
}
```

This cache persists across builds on reused nodes. It is also not thread-safe for concurrent access.

**After — engine-managed lifetime:**

```csharp
public class MyTask : Task
{
    private const string CacheKey = "MyNamespace.MyTask.Cache";
    private static readonly object s_cacheLock = new();

    public override bool Execute()
    {
        var engine4 = (IBuildEngine4)BuildEngine;

        var cache = (ConcurrentDictionary<string, string>)engine4.GetRegisteredTaskObject(
            CacheKey, RegisteredTaskObjectLifetime.Build);
        if (cache is null)
        {
            lock (s_cacheLock)
            {
                cache = (ConcurrentDictionary<string, string>)engine4.GetRegisteredTaskObject(
                    CacheKey, RegisteredTaskObjectLifetime.Build);
                if (cache is null)
                {
                    cache = new ConcurrentDictionary<string, string>();
                    engine4.RegisterTaskObject(
                        CacheKey, cache,
                        RegisteredTaskObjectLifetime.Build,
                        allowEarlyCollection: true);
                }
            }
        }

        cache[key] = value;
        ...
    }
}
```

The cache is now scoped to a single build and automatically discarded when the build completes.

Alternatively, a **lock-free** version of the same pattern takes advantage of the fact that `RegisterTaskObject` is thread-safe and only keeps the first registration for a given key — subsequent calls are ignored. After registering, re-read with `GetRegisteredTaskObject` to obtain the authoritative instance:

```csharp
var cache = (ConcurrentDictionary<string, string>)engine4.GetRegisteredTaskObject(
    CacheKey, RegisteredTaskObjectLifetime.Build);
if (cache is null)
{
    engine4.RegisterTaskObject(
        CacheKey, new ConcurrentDictionary<string, string>(),
        RegisteredTaskObjectLifetime.Build,
        allowEarlyCollection: true);
    // Re-read to get the authoritative instance in case another
    // task registered first.
    cache = (ConcurrentDictionary<string, string>)engine4.GetRegisteredTaskObject(
        CacheKey, RegisteredTaskObjectLifetime.Build);
}
```


> **Important:** When multiple tasks share a static field, for example through a utility class, migrating to `RegisterTaskObject` requires that _all_ tasks using the same key are migrated together. If some tasks are migrated while others continue to run in a separate task host process, the migrated tasks will use the engine-managed object while the non-migrated tasks will still use the static field, resulting in inconsistent behavior.

#### Cleanup-on-Dispose Pattern

When the previous `RegisterTaskObject` approach cannot be used — for example, when utility classes or helper methods use static caches but lack access to `IBuildEngine` — the recommended alternative is to keep the static field and register a disposable wrapper that clears it when the build ends:

```csharp
internal static class MyHelper
{
    // Static cache accessed by helper methods that have no IBuildEngine.
    private static readonly ConcurrentDictionary<string, string> s_cache = new();
    internal static void ClearCache() => s_cache.Clear();
}

public class MyTask : Task
{
    private const string CleanerKey = "MyNamespace.MyTask.CacheCleaner";

    public override bool Execute()
    {
        // Register a one-time cleanup wrapper so the static cache is
        // cleared when the build ends and does not leak into future builds.
        var engine4 = (IBuildEngine4)BuildEngine;
        if (engine4.GetRegisteredTaskObject(CleanerKey, RegisteredTaskObjectLifetime.Build) is null)
        {
            // If another task instance races ahead, only one registration wins.
            // This is safe: at least one cleanup wrapper will be registered.
            engine4.RegisterTaskObject(
                CleanerKey,
                new CacheCleanup(MyHelper.ClearCache),
                RegisteredTaskObjectLifetime.Build,
                allowEarlyCollection: false);
        }
        ...
    }

    /// <summary>
    /// Invokes a cleanup delegate when disposed. Register with
    /// RegisterTaskObject so MSBuild calls Dispose at end of build.
    /// </summary>
    private sealed class CacheCleanup(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
```

The same pattern must be applied to third-party libraries that maintain their own static state — a task may register a cleanup wrapper that calls the library's cache-clearing API.

### Guidelines for Task Authors

1. **Set `allowEarlyCollection: true`** when the cached data can be safely recreated. This lets MSBuild reclaim memory under pressure. Use `false` only for objects that must survive the entire build (e.g., cleanup wrappers, long-lived connections).
2. **Use a stable, unique key.** A `const string` with the fully-qualified task name avoids collisions (e.g., `"MyNamespace.MyTask.Cache"`).
3. **Handle null returns.** `GetRegisteredTaskObject` returns null when no object is registered under the key, or when a previously registered object was disposed through early collection.
4. **Objects used by multiple task invocations must be thread-safe** in multithreaded builds, since multiple task instances may retrieve and use the same object concurrently.

## Appendix: Alternatives

This appendix collects alternative approaches considered during design.

### Alternative Approach: API Hooking

An alternative approach to the `TaskEnvironment` API could be to use API hooking (such as Microsoft Detours) to automatically virtualize global process state without requiring any changes from task authors.

The main advantages of API hooking include requiring no action from task authors since existing tasks would work without modification or recompilation, and having no compatibility concerns with older MSBuild versions. However, it would be a Windows-only solution, making it unsuitable for cross-platform scenarios. 

### Alternative to Attribute-Based Thread-Safe Capability Declaration

We considered making the thread-safety signal using the task declaration (for example, a `ThreadSafe="true"` attribute on `UsingTask`) so that project authors could declare compatibility without changing task assemblies. However, because older MSBuild versions treat unknown attributes in task declarations as errors, this approach would require updating older MSBuild versions or servicing them to ignore the attribute. 