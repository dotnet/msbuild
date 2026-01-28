---
name: multithreaded-task-migration
description: Guide for migrating MSBuild tasks to the multithreaded mode support. Use this when asked to convert tasks to thread-safe versions, implement IMultiThreadableTask, or add TaskEnvironment support to tasks.
---

# Migrating MSBuild Tasks to Multithreaded API

This skill guides you through migrating MSBuild tasks to support multithreaded execution by implementing `IMultiThreadableTask` and using `TaskEnvironment`.

## Overview

MSBuild's multithreaded execution model requires tasks to avoid global process state (working directory, environment variables). Thread-safe tasks declare this capability by annotating with  `MSBuildMultiThreadableTask` and use `TaskEnvironment` provided by `IMultiThreadableTask` for safe alternatives.

## Migration Steps

### Step 1: Update Task Class Declaration

a. add the attribute
b. AND implement the interface if it's necessary to use TaskEnvironment APIs.

```csharp
[MSBuildMultiThreadableTask]
public class MyTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    ...
}
```

### Step 2: Replace Path Resolution APIs

**Critical**: absolutize all path strings with `TaskEnvironment.GetAbsolutePath()` before their use.
`GetAbsolutePath()` throws `ArgumentNullException` for null inputs and throws for empty inputs. Callers are responsible for validating inputs before calling absolutization APIs.

```csharp
// BEFORE - Uses working directory (UNSAFE)
string absolutePath = Path.GetFullPath(relativePath);

// AFTER - Uses project directory (SAFE)
AbsolutePath absolutePath = TaskEnvironment.GetAbsolutePath(relativePath);
```

The `AbsolutePath` struct:
- Has `Value` property returning the absolute path string
- Has `OriginalValue` property preserving the input path
- Is implicitly convertible to `string` for File/Directory API compatibility
- **Throws `ArgumentNullException` for null paths** 
- **Throws for empty paths** 

#### Note:
GetFullPath also resolves relative segments so it should be called after absolutization if that behavior is required, and if a method is explicitly fixing directory separators it should be also preserved instead of replacing with GetAbsolutePath directly.
The goal is MAXIMUM compatibility so think about these edge cases so it behaves the same as before.

### Step 3: Replace Environment Variable APIs

```csharp
// BEFORE (UNSAFE)
string value = Environment.GetEnvironmentVariable("VAR");
Environment.SetEnvironmentVariable("VAR", "value");

// AFTER (SAFE)
string value = TaskEnvironment.GetEnvironmentVariable("VAR");
TaskEnvironment.SetEnvironmentVariable("VAR", "value");
```

### Step 4: Replace Process Start APIs

```csharp
// BEFORE (UNSAFE - inherits process state)
var psi = new ProcessStartInfo("tool.exe");

// AFTER (SAFE - uses task's isolated environment)
var psi = TaskEnvironment.GetProcessStartInfo();
psi.FileName = "tool.exe";
```

### Step 5: Use Absolute Paths with File APIs

All file system operations must use absolute paths. The analyzer (MSB9997) will warn on potentially relative paths:

```csharp
// UNSAFE - may resolve relative to wrong directory
File.Exists(somePath);

// SAFE - explicitly absolutized
File.Exists(TaskEnvironment.GetAbsolutePath(somePath));

// ALSO SAFE - already absolute (e.g., from FileInfo.FullName)
File.Exists(fileInfo.FullName);
```

## Updating Unit Tests

**Every test creating a task instance must set TaskEnvironment.** Use `TaskEnvironmentHelper.CreateForTest()`:

```csharp
// BEFORE
var task = new Copy
{
    BuildEngine = new MockEngine(true),
    SourceFiles = sourceFiles,
    DestinationFolder = new TaskItem(destFolder),
};

// AFTER
var task = new Copy
{
    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
    BuildEngine = new MockEngine(true),
    SourceFiles = sourceFiles,
    DestinationFolder = new TaskItem(destFolder),
};
```

### Testing Exception Cases

Tasks must handle null/empty path inputs properly.

```csharp
[Fact]
public void Task_WithNullPath_Throws()
{
    var task = CreateTask();
    
    Should.Throw<ArgumentNullException>(() => task.ProcessPath(null!));
}
```

## APIs to Avoid

### Critical Errors (No Alternative)
- `Environment.Exit()`, `Environment.FailFast()` - Return false or throw instead
- `Process.GetCurrentProcess().Kill()` - Never terminate process
- `ThreadPool.SetMinThreads/MaxThreads` - Process-wide settings
- `CultureInfo.DefaultThreadCurrentCulture` (setter) - Affects all threads
- `Console.*` - Interferes with logging

### Requires TaskEnvironment
- `Environment.CurrentDirectory` → `TaskEnvironment.ProjectDirectory`
- `Environment.GetEnvironmentVariable` → `TaskEnvironment.GetEnvironmentVariable`
- `Environment.SetEnvironmentVariable` → `TaskEnvironment.SetEnvironmentVariable`
- `Path.GetFullPath` → `TaskEnvironment.GetAbsolutePath`
- `Process.Start`, `ProcessStartInfo` → `TaskEnvironment.GetProcessStartInfo`

### File APIs Need Absolute Paths
- `File.*`, `Directory.*`, `FileInfo`, `DirectoryInfo`, `FileStream`, `StreamReader`, `StreamWriter`
- All path parameters must be absolute

### Potential Issues (Review Required)
- `Assembly.Load*`, `LoadFrom`, `LoadFile` - Version conflicts
- `Activator.CreateInstance*` - Version conflicts

## Practical Notes from Implementation Experience


### Null/Empty Path Handling Evolution


```csharp
// Current behavior - always throws for null/empty
TaskEnvironment.GetAbsolutePath(null);    // Throws ArgumentNullException
TaskEnvironment.GetAbsolutePath(string.Empty);  // Throws

// Caller responsibility to validate
if (!string.IsNullOrEmpty(path))
{
    AbsolutePath absolute = TaskEnvironment.GetAbsolutePath(path);
    ...
}
```

### TaskEnvironment is Not Thread-Safe

If your task spawns multiple threads internally, you must synchronize access to `TaskEnvironment`. However, each task instance gets its own environment, so no synchronization with other tasks is needed.

## Checklist

- [ ] Task is annotated with `MSBuildMultiThreadableTask` attribute and implements `IMultiThreadableTask` if TaskEnvironment APIs are required
- [ ] All environment variable access uses `TaskEnvironment` APIs
- [ ] All process spawning uses `TaskEnvironment.GetProcessStartInfo()`
- [ ] All file system APIs receive absolute paths
- [ ] No use of `Environment.CurrentDirectory`
- [ ] All tests set `TaskEnvironment = TaskEnvironmentHelper.CreateForTest()`
- [ ] Tests verify exception behavior for null/empty paths
- [ ] No use of forbidden APIs (Environment.Exit, etc.)
