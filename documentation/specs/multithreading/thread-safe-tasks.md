# Thread-Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization. With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to ensure thread safety when multiple tasks access shared process state simultaneously.

To enable this multithreaded execution model, tasks will declare their thread-safety capabilities. Thread-safe tasks must avoid using APIs that modify or depend on global process state, as this could cause conflicts when multiple tasks execute concurrently. See [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md) for detailed guidelines. Task authors will also get access to an `ExecutionContext` that provides safe alternatives to global process state APIs. For example, task authors should use `ExecutionContext.GetAbsolutePath()` instead of `Path.GetFullPath()` to ensure correct path resolution in multithreaded scenarios. 

Tasks that are not thread-safe can still participate in multithreaded builds. MSBuild will execute these tasks in separate TaskHost processes to provide process-level isolation.

## Alternative Approach: API Hooking

An alternative approach to the ExecutionContext API would be to use API hooking (such as Microsoft Detours) to automatically virtualize global process state without requiring any changes from task authors.

**Pros of API Hooking:**
- No action required from task authors - existing tasks work without modification or recompilation
- No compatibility concerns with older MSBuild versions

**Cons of API Hooking:**
- Windows-only solution (Detours is platform-specific)
- More complex implementation
- Potential performance overhead from API hooking

## Thread-Safe Capability Indicators

Task authors can declare thread-safe capabilities through various approaches. These approaches fall into two categories:

1. **ExecutionContext-enabled approaches** - Provide access to thread-safe APIs through `ExecutionContext`
2. **Compatibility bridge approaches** - Allow existing tasks to run in multithreaded mode without code changes

Tasks that use ExecutionContext-enabled approaches cannot load in older MSBuild versions that do not support the multithreading mode feature, so they are dropping support for older MSBuild versions. To address this challenge, MSBuild provides **compatibility bridge approaches** that allow legacy tasks to participate in multithreaded builds under specific conditions:
- The task must not modify global process state (environment variables, working directory)
- The task must not depend on process-wide state, including relative path resolution
- The task will not have access to `ExecutionContext` APIs

This bridge enables existing tasks to benefit from multithreaded execution without requiring code changes.

So, task authors who need to support older MSBuild versions will have three choices:
1. **Maintain separate implementations** - Create and support both thread-safe and legacy versions of the same task, using options 1 or 3 below
2. **Use compatibility bridge approaches** - Rely on MSBuild's ability to run legacy tasks in multithreaded mode without ExecutionContext access, using options 2 or 4 below
3. **Accept reduced performance** - Tasks will execute more slowly than their thread-safe versions because they must run in a separate TaskHost process

### Interface-Based Thread-Safe Declaration (Option 1)

Tasks indicate thread-safety capabilities by implementing the `IThreadSafeTask` interface.

```csharp
public interface IThreadSafeTask : ITask
{
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

**Pros:**
- Clear, explicit opt-in mechanism for thread safety
- Follows established MSBuild patterns (e.g., `ICancelableTask`)
- Provides access to ExecutionContext APIs

**Cons:**
- Not compatible with older MSBuild versions. The task will not be able to load, so authors will need to create separate task classes, and even separate assemblies if a wide range of MSBuild versions needs to be supported.

Task authors who want to support older MSBuild versions need to:
- Maintain both thread-safe and legacy implementations.
- Use conditional task declarations based on MSBuild version to select which assembly to load the task from. This is necessary because task declarations don't support aliases - the task name serves as both the identifier in project files and the class name to locate in the assembly.
- Handle version detection and selection logic.

**Note:** Consider backporting `IThreadSafeTask` to MSBuild 17.14 for graceful failure when the interface is used.

#### Alternative design: Parameter-based approach

An alternative approach would be to use a parameter-based interface design:

```csharp
public interface IThreadSafeTask
{
    bool Execute(ITaskExecutionContext context);
}
```

**Pros:**
- ExecutionContext is explicitly visible in the method signature

**Cons:**
- Less extensible for future features like async support, would require multiple overloads if additional features are added

**Question:** For this parameter-based approach, we need to decide whether MSBuild should support async task execution (using `ExecuteAsync` methods) to avoid creating multiple overloads of the Execute method in the `IThreadSafeTask` interface.

**Note:** Both approaches can either extend `ITask` or be independent. It seems more aligned with SOLID principles to extend `ITask` in the property-based approach and have an independent interface in the parameter-based approach.

**Note:** An independent interface allows task authors to have two versions of the same task in one assembly using different namespaces. Older MSBuild versions will locate and use only the `ITask` version, while newer MSBuild versions will locate both and call the appropriate version.

**Limitations:**
- Breaks when tasks are located by full type name
- Unclear how this scales with future task interface features  

### Attribute-Based Capability Declaration (Option 2)

Task authors will indicate thread-safety capabilities using attributes. Tasks marked with this attribute can run in multithreaded builds but do not have access to the `ExecutionContext` APIs.

```csharp
[TaskCapability("ThreadSafe")] // Or just [ThreadSafe]
public class MyTask : Task {...}
```

**Pros:**
- Zero code changes required for existing tasks
- Full backward compatibility with older MSBuild versions (attribute ignored in old versions)
- Simple declaration mechanism
- Immediate adoption path for compatible tasks

**Cons:**
- No access to ExecutionContext APIs (e.g., `ExecutionContext.GetAbsolutePath()`)
- Task authors must manually verify thread safety
- No MSBuild assistance for thread-safe operations
- Risk of false declarations if tasks aren't truly thread-safe

### Alternative, Parameter-Based Execute Method Overloads (Option 3)

Task authors implement Execute method overloads that accept additional parameters, without relying on the interface. MSBuild automatically calls the most advanced overload it can support, while older MSBuild versions always call the basic overload.

```csharp
public class MyTask : Task
{
    // Legacy implementation for older MSBuild versions
    public bool Execute()
    { 
        // Basic implementation without ExecutionContext
    }

    // Thread-safe implementation for newer MSBuild versions
    public bool Execute(ITaskExecutionContext context)
    { 
        // Enhanced implementation with ExecutionContext
    }
}
```

**Pros:**
- Full backward compatibility with older MSBuild versions
- No conditional task declarations required
- Access to ExecutionContext APIs in newer MSBuild versions

**Cons:**
- Execute method structure is not formalized in any API, making it very confusing.
- Limited IntelliSense support for legacy implementation when targeting newer MSBuild APIs
- Unclear which overload will be called in different scenarios
- Unclear how to scale this approach if we add more features

**Note:** If an older MSBuild version cannot load the `Execute(ITaskExecutionContext context)` method due to missing types, it may automatically fall back to the `Execute()` method if it is available.

**Question:** Which set of options above should MSBuild support?

### Alternative, XML Attribute-Based Thread-Safe Declaration (Option 4)

Task authors or task users declare thread-safety capabilities using an attribute in the task declaration. Tasks marked with this attribute can run in multithreaded builds but do not have access to the `ExecutionContext` APIs.

```xml
<UsingTask TaskName="MyTask" AssemblyFile="MyTask.dll" ThreadSafe="true" />
```

**Pros:**
- Zero code changes required for existing tasks
- Simple declaration mechanism
- Immediate adoption path for compatible tasks
- Some backward compatibility with older MSBuild versions (see Implementation Considerations)

**Cons:**
- No access to ExecutionContext APIs (e.g., `ExecutionContext.GetAbsolutePath()`)
- Task authors must manually verify thread safety
- No MSBuild assistance for thread-safe operations
- Risk of false declarations if tasks aren't truly thread-safe

#### Implementation Considerations

**Option A:** Service older MSBuild versions to ignore the `ThreadSafe` attribute and prevent errors. This is necessary because older MSBuild versions throw errors for unknown attributes, even in conditional task declarations.

**Option B:** Use new assembly factory types (e.g., `AssemblyThreadCompatibleTaskFactory`) to avoid servicing older versions. Task declarations must still be conditional based on MSBuild version since these factories don't exist in older versions.

## ExecutionContext API

The `ExecutionContext` provides thread-safe alternatives to APIs that use global process state, enabling tasks to execute safely in a multithreaded environment.

```csharp
public interface IThreadSafeTask : ITask
{
    TaskExecutionContext ExecutionContext { get; set; }
}

public abstract class TaskExecutionContext
{ 
    public abstract AbsolutePath CurrentDirectory { get; set; }

    public abstract AbsolutePath GetAbsolutePath(string path);
    
    public abstract string? GetEnvironmentVariable(string name);
    public abstract IReadOnlyDictionary<string, string> GetEnvironmentVariables();
    public abstract void SetEnvironmentVariable(string name, string? value);

    public abstract ProcessStartInfo GetProcessStartInfo();
    public abstract Process StartProcess(ProcessStartInfo startInfo);
    public abstract Process StartProcess(string fileName);
    public abstract Process StartProcess(string fileName, IEnumerable<string> arguments);
}
```

The `ExecutionContext` that MSBuild provides is not thread-safe. Task authors who spawn multiple threads within their task implementation must provide their own synchronization when accessing the execution context from multiple threads. However, each task receives its own isolated context object, so synchronization with other concurrent tasks is not required.

### Path Handling Types

To prevent common thread-safety issues related to path handling, we introduce path types that are implicitly convertible to string:

```csharp
public struct AbsolutePath
{
    public string Path { get; }
    
    // Will be banned for use in tasks by analyzers
    public AbsolutePath(string path) { }
    
    public AbsolutePath(string path, AbsolutePath basePath) { }
    
    public static implicit operator string(AbsolutePath path) { }
}

public struct RelativePath
{
    public string Path { get; }
    
    public RelativePath(string path) { }
    
    public AbsolutePath ToAbsolute(AbsolutePath? basePath = null) { }
    
    public static implicit operator string(RelativePath path) { }
}
```

Both types convert implicitly to string for seamless integration with existing File/Directory APIs. This approach may require adjustment if similar concepts are added to the standard .NET API.

**TODO:** Consider adding conversion methods for `ITaskItem` integration.

### Usage Example

```csharp
public bool Execute(...)
{
    // Use APIs provided by ExecutionContext
    string envVar = ExecutionContext.GetEnvironmentVariable("EnvVar");
       
    // Convert string properties to strongly-typed paths and use them in standard File/Directory APIs
    AbsolutePath path = ExecutionContext.GetAbsolutePath("SomePath");
    string content = File.ReadAllText(path);
    ...
}
```

### Alternative: Interfaces
Alternatively, ExecutionContext could be an implementation of the `ITaskExecutionContext` interface.

```csharp
public interface ITaskExecutionContext
{
    // Same methods as in abstract class TaskExecutionContext
}
```

To handle future updates without breaking existing implementations, we will create versioned interfaces:

```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    // New methods for version 2
}
```

**Cons:**
- Not backward compatible for customers who extend the class, while an abstract class can have a default implementation.
- Worse performance compared to abstract classes.

**Question:** What are the other pros and cons of these two approaches? Which option is better?

