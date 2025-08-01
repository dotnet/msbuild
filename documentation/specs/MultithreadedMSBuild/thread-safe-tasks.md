# Thread-Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization. With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to ensure thread safety when multiple tasks access shared process state simultaneously.

To enable this multithreaded execution model, tasks can declare their thread-safety capabilities. Thread-safe tasks must avoid using APIs that modify or depend on global process state, as this could cause conflicts when multiple tasks execute concurrently. See [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md) for detailed guidelines. Task authors will also gain access to an `ExecutionContext` that provides safe alternatives to global process state APIs. For example, task authors should use `ExecutionContext.GetAbsolutePath()` instead of `Path.GetFullPath()` to ensure correct path resolution in multithreaded scenarios.

## Thread-Safe Capability Indicators

MSBuild can indicate that a task has thread-safe capabilities through various approaches. Some of them will provide access to `ExecutionContext`. However, tasks that use `ExecutionContext` APIs are not compatible with older MSBuild versions that don't support these features: loading the `Execute` function will fail in older MSBuild versions. So, anyone attempting to participate in multithreaded builds efficiently will necessarily cut off a long tail of MSBuild/IDE support. To ease this transition, MSBuild will initially provide a compatibility bridge that allows legacy tasks to participate in multithreaded builds if they don't modify global process state. This bridge enables existing tasks to benefit from multithreaded execution without requiring code changes, though they won't have access to `ExecutionContext` APIs.

Task authors who need to support older MSBuild versions have three choices:
1. **Maintain separate implementations** - Create both thread-safe and legacy versions
2. **Use compatibility bridge approaches** - Rely on MSBuild's ability to run legacy tasks in multithreaded mode without ExecutionContext access
3. **Accept reduced performance** - Tasks will execute slower than their thread-safe version because they must run in a separate TaskHost process.

For legacy tasks to run safely in multithreaded builds, they must:
- Not modify environment variables or current working directory
- Not depend on relative path resolution from current working directory
- Not use static fields or other shared state without proper synchronization
- Not rely on specific environment variable values being set

We can implement any combinations of the options below. At least one of them should be the compatibility bridge option: compatible with older MSBuild versions. 

### Option 1: Interface-Based Thread-Safe Declaration

Task authors indicate thread-safety capabilities by implementing the `IThreadSafeTask` interface.

**Pros:**
- Clear, explicit opt-in mechanism for thread safety
- Follows established MSBuild patterns (e.g., `ICancelableTask`)
- Provides access to ExecutionContext APIs

**Cons:**
- Not compatible with older MSBuild versions. The task will not be able to load, so authors will need to create separate task classes, and even separate assemblies if a wide range of MSBuild versions needs to be supported.

Task authors who want to support older MSBuild versions need to:
- Maintain both thread-safe and legacy implementations
- Use conditional task declarations based on MSBuild version to select which assembly to load the task from. This is necessary because task declarations don't support aliases - the task name serves as both the identifier in project files and the class name to locate in the assembly.
- Handle version detection and selection logic

**Note:** Consider backporting `IThreadSafeTask` to MSBuild 17.14 for graceful failure when the interface is used.

**Option A:** Property-based approach
```csharp
public interface IThreadSafeTask : ITask
{
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

**Option B:** Parameter-based approach
```csharp
public interface IThreadSafeTask : ITask
{
    bool Execute(ITaskExecutionContext context);
}
```

**Pros of Option A:**
- Better feature decoupling for future extensibility. For example, if we add async support, we need only one method (`ExecuteAsync()`) instead of two (`ExecuteAsync()` and `ExecuteAsync(ITaskExecutionContext context)`).

**Pros of Option B:**
- ExecutionContext is explicitly visible in the method signature

**Interface Inheritance:**
Both options can either extend `ITask` (as shown above) or be independent:
```csharp
public interface IThreadSafeTask
{
    // Same members as above
}
```

**Independent Interface Benefits:**
An independent interface allows task authors to have two versions of the same task in one assembly using different namespaces. Older MSBuild versions will locate and use only the `ITask` version, while newer MSBuild versions will locate both and call the appropriate version.

**Limitations:**
- Breaks when tasks are located by full type name
- Unclear how this scales with future task interface features

**Question:** Which option is better?  

### Option 2: Attribute-Based Capability Declaration

Task authors indicate thread-safety capabilities using attributes. Tasks marked with this attribute can run in multithreaded builds but do not have access to the `ExecutionContext` APIs.

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

### Option 3: XML Attribute-Based Thread-Safe Declaration

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

**Option A:** Service older MSBuild versions to ignore the `ThreadSafe` attribute and prevent errors. This is necessary because older MSBuild versions throw errors for unknown attributes even in conditional task declarations.

**Option B:** Use new assembly factory types (e.g., `AssemblyThreadCompatibleTaskFactory`) to avoid servicing older versions. Task declarations must still be conditional based on MSBuild version since these factories don't exist in older versions.

### Option 4: Parameter-Based Execute Method Overloads

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
- Execute method structure not formalized in any API. It is very confusing
- Limited IntelliSense support for legacy implementation when targeting newer MSBuild APIs
- Unclear which overload will be called in different scenarios
- Unclear how to scale this approach in case we add more features

**Note:** If an older MSBuild version cannot load the `Execute(ITaskExecutionContext context)` method due to missing types, it may automatically fall back to the `Execute()` method if it is available.

## ExecutionContext API

The `ExecutionContext` provides thread-safe alternatives to global process state APIs, enabling tasks to execute safely in multithreaded environments.

```csharp
public interface ITaskExecutionContext
{
    AbsolutePath CurrentDirectory { get; set; }

    AbsolutePath GetAbsolutePath(string path);
    
    string? GetEnvironmentVariable(string name);
    IReadOnlyDictionary<string, string> GetEnvironmentVariables();
    void SetEnvironmentVariable(string name, string? value);

    ProcessStartInfo GetProcessStartInfo();
    Process StartProcess(ProcessStartInfo startInfo);
    Process StartProcess(string fileName);
    Process StartProcess(string fileName, IEnumerable<string> arguments);
}
```

The `ITaskExecutionContext` is not thread-safe for performance reasons. Task authors who spawn multiple threads within their task implementation must provide their own synchronization when accessing the execution context from multiple threads. However, each task receives its own isolated context object, so synchronization with other concurrent tasks is not required.

### Path Handling Types

To prevent common thread-safety issues related to path handling, we introduce strongly-typed path classes that are implicitly convertible to string:

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

## Interface Evolution Strategy

To handle future updates without breaking existing implementations, we will create versioned interfaces:

```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    // New methods for version 2
}
```

### Alternative: Abstract Classes

Alternatively, we can use abstract classes:

```csharp
public interface IThreadSafeTask : ITask
{
    TaskExecutionContext ExecutionContext { get; set; }
}

public abstract class TaskExecutionContext
{ 
    // Same methods as the interface, with default implementations
}
```

**Note:**
- Default implementations allow backward compatibility for customers who extend the class.

