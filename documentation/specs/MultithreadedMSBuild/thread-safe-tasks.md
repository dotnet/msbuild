# Thread Safe Tasks

## Overview

MSBuild's current execution model assumes that tasks have exclusive control over the entire process during execution. This allows tasks to freely modify global process state such as environment variables, the current working directory, and other process-level resources. This design works well for MSBuild's approach of executing builds in separate processes for parallelization.

With the introduction of multithreaded execution within a single MSBuild process, multiple tasks can now run concurrently. This requires a new task design to prevent race conditions and ensure thread safety when multiple tasks access shared process state simultaneously.

To enable this multithreaded execution model, we introduce the `IThreadSafeTask` interface that tasks can implement to declare their thread-safety capabilities. Tasks implementing this interface must avoid using APIs that modify or depend on global process state, as such usage could cause conflicts when multiple tasks execute concurrently, see [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

Task authors should use the `ExecutionContext` property provided by the `IThreadSafeTask` interface to access thread-safe APIs for operations that would otherwise use global process state. For example, use `ExecutionContext.Path.GetFullPath(relativePath)` instead of the standard `Path.GetFullPath(relativePath)`.

## Option 1: Structured Interfaces

```csharp
public interface IThreadSafeTask<TExecutionContext> : ITask
    where TExecutionContext : ITaskExecutionContext
{
    TExecutionContext ExecutionContext { get; set; }
}
```

The `ITaskExecutionContext` provides tasks with access to what was in multi-process mode the global process state, such as environment variables and working directory:
```csharp
public interface ITaskExecutionContext
{    
    string CurrentDirectory { get; set; }

    IEnvironment Environment { get; }

    IPath Path { get; }

    IFile File { get; }

    IDirectory Directory { get; }
}
```

The `IEnvironment` provides thread-safe access to environment variables:
```csharp
public interface IEnvironment
{
    string? GetEnvironmentVariable(string name);
    
    Dictionary<string, string?> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
}
```

Thread-safe alternative to `System.IO.Path` class:
```csharp
public interface IPath
{
    string GetFullPath(string path);
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.File` class:

```csharp
public interface IFile
{
    bool Exists(string path);
    string ReadAllText(string path);
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.Directory` class:

```csharp
public interface IDirectory
{
    bool Exists(string path);
    DirectoryInfo CreateDirectory(string path);
    ... // Complete list of methods can be find below
}
```

### Interface Versioning Pattern

To handle future updates to interfaces without breaking existing implementations, we wil use a versioning pattern. 

```csharp
public interface IFile2 : IFile
{
    string ReadAllText(string path, Encoding encoding)
    ... // Other new methods added in version 2
}
```

Unfortunatelly, `ITaskExecutionContext` will need a version update as well.
```csharp
public interface ITaskExecutionContext2 : ITaskExecutionContext
{
    new IPath2 Path { get; }
}
```

### Usage Examples

```csharp
// Tasks should use minimum `ITaskExecutionContext` version that provides the needed functionality
public class MyTask : IThreadSafeTask<ITaskExecutionContext>
{
    public ITaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt");
        return true;
    }
}

// Tasks that need newer functionality
public class AdvancedTask : IThreadSafeTask<ITaskExecutionContext2>
{
    public ITaskExecutionContext2 ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt", Encoding.UTF8);
        return true;
    }
}
```
**Note** During the loading of the task assembly, we can check whether the needed version of the `ITaskExecutionContext` is present and gracefully fail if not.

**Note** Consider backporting this check to 17.14 branch as well.

## Option 2: Abstract Classes

This approach uses abstract classes instead of interfaces.

```csharp
public interface IThreadSafeTask : ITask
{
    TaskExecutionContext ExecutionContext { get; set; }
}
```

### TaskExecutionContext Abstract Class

The `TaskExecutionContext` provides tasks with access to what was in multi-process mode the global process state, such as environment variables and working directory:

```csharp
public abstract class TaskExecutionContext
{
    public virtual string CurrentDirectory { get; set; }
    public virtual TaskEnvironment Environment { get; }
    public virtual TaskPath Path { get; }
    public virtual TaskFile File { get; }
    public virtual TaskDirectory Directory { get; }
}
```

The `TaskEnvironment` provides thread-safe access to environment variables:
```csharp
public abstract class TaskEnvironment
{
    public virtual string? GetEnvironmentVariable(string name) => throw new NotImplementedException();
    public virtual Dictionary<string, string?> GetEnvironmentVariables() => throw new NotImplementedException();
    public virtual void SetEnvironmentVariable(string name, string? value) => throw new NotImplementedException();
}
```

Thread-safe alternative to `System.IO.Path` class. 
```csharp
public abstract class TaskPath
{
    public virtual string GetFullPath(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

**Note** the default implementations allow backwords compatibility for the customers' that implement the class. 

Thread-safe alternative to `System.IO.File` class:
```csharp
public abstract class TaskFile
{    
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

Thread-safe alternative to `System.IO.Directory` class:
```csharp
public abstract class TaskDirectory
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual DirectoryInfo CreateDirectory(string path) => throw new NotImplementedException();
    ... // Complete list of methods can be find below
}
```

### Versioning Pattern

With abstract classes, there is no need to create a new type to add methods.

```csharp
public abstract class TaskFile
{
    public virtual bool Exists(string path) => throw new NotImplementedException();
    public virtual string ReadAllText(string path) => throw new NotImplementedException();
    
    // Method added to the class:
    public virtual string ReadAllText(string path, Encoding encoding) => throw new NotImplementedException();
    ... // Other methods can be added here
}
```

### Usage Examples

```csharp
public class MyTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt");
        return true;
    }
}

// Tasks that need newer functionality
public class AdvancedTask : IThreadSafeTask
{
    public TaskExecutionContext ExecutionContext { get; set; }
    
    public bool Execute()
    {
        var text = ExecutionContext.File.ReadAllText("file.txt", Encoding.UTF8);
        return true;
    }
}
```

**Question**: How can we check the version compatibility and gracefully fail if the required version is not available? It is not possible in the current design.

## Methods Reference

### Path Methods

- `bool Exists(string path)`
- `string GetFullPath(string path)`

### File Methods

**TODO** Make a list

**Question** In net core and net framework (and in different versions) there is different set of the functions. Which exactly should we take?

**Idea** We can use info from apisof.net to identify the most used API and we can drop not much used.

### Directory Methods

**TODO** Make a list

## Notes

**Note**: Our implementations should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.

**Question**: We want to prevent customers from setting or modifying the ExecutionContext, what is the good way of doing that?