# Thread-Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization. With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to ensure thread safety when multiple tasks access shared process state simultaneously.

To enable this multithreaded execution model, we introduce the `IThreadSafeTask` interface that tasks can implement to declare their thread-safety capabilities. Tasks implementing this interface must avoid using APIs that modify or depend on global process state, as this could cause conflicts when multiple tasks execute concurrently. See [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md) for detailed guidelines. At the same time, the `IThreadSafeTask` interface provides access to the `ExecutionContext` property that allows safe access to global process state. For example, task authors should use `ExecutionContext.GetAbsolutePath(relativePath)` instead of the standard `Path.GetFullPath(relativePath)` to ensure correct path resolution.

## Design

```csharp
public interface IThreadSafeTask : ITask
{
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

**Note:** Consider backporting the `IThreadSafeTask` interface to the 17.14 branch to allow a graceful fail when tasks attempt to use it.

The execution context provides essential methods for thread-safe task execution, replacing direct access to global process state:

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

**Note:** The `ITaskExecutionContext` will not be thread-safe for performance reasons. Task authors who spawn multiple threads within their task implementation must provide their own synchronization when accessing the execution context from multiple threads. However, each thread node has its own isolated context object provided to the tasks, so task authors do not need to worry about synchronization with other tasks running concurrently in different thread nodes.

To help task authors avoid thread-safety issues related to path handling, we introduce `AbsolutePath` and `RelativePath` classes that are implicitly convertible to string. 

```csharp
public class AbsolutePath
{
    public string Path { get; }
    
    // Will be banned for use in tasks by analyzers
    public AbsolutePath(string path) { }
    
    public AbsolutePath(string path, AbsolutePath basePath) { }
    
    public static implicit operator string(AbsolutePath path) { }
}

public class RelativePath
{
    public string Path { get; }
    
    public RelativePath(string path) { }
    
    public AbsolutePath ToAbsolute(AbsolutePath? basePath = null) { }
    
    public static implicit operator string(RelativePath path) { }
}
```

**Note:** Usage of `AbsolutePath` and `RelativePath` may need adjustment if similar concepts are implemented in the standard .NET API in the future.

**TODO:** Consider having conversions for `ITaskItem`.

### Interface Evolution Strategy

To handle future updates without breaking existing implementations, we will create versioned interfaces:

```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    // New methods for version 2
}
```

#### Alternative: Abstract Classes

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
- However, version compatibility checking is not possible with this design.

## Authoring Thread-Safe Tasks

### Supporting Running 'classic' MSBuild Tasks in Thread Node

Task authors implementing `IThreadSafeTask` need to:

- Update package dependencies to support thread-safe APIs
- Create and maintain both thread-safe and legacy versions if support for older MSBuild versions is necessary
- Implement logic to choose appropriate task implementations based on MSBuild capabilities

These requirements effectively cut off support for a long tail of MSBuild and IDE versions, creating a barrier to adoption.

To allow easier adoption, MSBuild will enable the thread worker node to either recognize and wrap legacy tasks in thread-based semantics or run the 'classic' tasks themselves. This will allow task authors who are confident their tasks don't perform stateful actions to participate in multithreaded builds without sacrificing legacy support or needing to support multiple versions.

Legacy tasks can run in multithreaded builds if they:
- Do not change environment variables or current working directory
- Do not depend on relative path resolution from current working directory
- Do not use static fields or other shared state unsafely
- Do not rely on specific environment variable values being set

Task authors and users who would like to utilize this capability will need to add the `ThreadSafe` attribute to the task declaration:
```xml
  <UsingTask TaskName="MyTask" AssemblyFile="MyTask.dll" ThreadSafe="true" Condition="'$(MSBuildSupportsThreadSafeTasks)' != 'true'" />
```

**Note** We will need to service supported branches so that older versions of MSBuild ignore the `ThreadSafe` attribute and do not throw the `MSB4066` error. For MSBuild versions that would not be serviced, task authors will need to conditionally import the file with task declarations based on the running MSBuild version.

**Note** Alternatively, we can introduce new types of assembly factories (`AssemblyThreadCompatibleTaskFactory` and others) to avoid the error. The task declarations still need to be conditional, but this would spare us the need to service older versions. 

This support would be enabled by default for the first release of Multithreaded MSBuild, but the goal should be to deprecate it. We will disable the compatibility bridge by default eventually. We will use build-level telemetry to track 'classic' MSBuild tasks running in multithreaded mode to monitor `IThreadSafeTask` adoption trends.

## Examples
Basic `IThreadSafeTask` Example:
```csharp
public class MyTask : IThreadSafeTask
{
    public bool Execute()
    {
        // Use APIs provided by ExecutionContext
        string envVar = ExecutionContext.GetEnvironmentVariable("EnvVar");
       
        // Convert string properties to strongly-typed paths and use them in standard File/Directory APIs
        AbsolutePath path = ExecutionContext.GetAbsolutePath("SomePath");
        string content = File.ReadAllText(path);
        
        return true;
    }
}
```

Conditional Task Declaration Example:
```xml
<Project>
  <PropertyGroup>
    <MSBuildSupportsThreadSafeTasks Condition="'$(MSBuildVersion)' >= '17.15'">true</MSBuildSupportsThreadSafeTasks>
  </PropertyGroup>

  <UsingTask 
    TaskName="MyTask" 
    AssemblyFile="$(MSBuildThisFileDirectory)../lib/netstandard2.0/ThreadSafe/MyTask.dll" 
    Condition="'$(MSBuildSupportsThreadSafeTasks)' == 'true'" />
    
  <UsingTask 
    TaskName="MyTask" 
    AssemblyFile="$(MSBuildThisFileDirectory)../lib/netstandard2.0/Legacy/MyTask.dll" 
    Condition="'$(MSBuildSupportsThreadSafeTasks)' != 'true'" />
</Project>
```
