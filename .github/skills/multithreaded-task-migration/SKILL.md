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

### Step 2: Absolutize Paths Before File Operations

**Critical**: All path strings must be absolutized with `TaskEnvironment.GetAbsolutePath()` before use in file system APIs. This ensures paths resolve relative to the project directory, not the process working directory.

```csharp
// BEFORE - File.Exists uses process working directory for relative paths (UNSAFE)
if (File.Exists(inputPath))
{
    string content = File.ReadAllText(inputPath);
}

// AFTER - Absolutize first, then use in file operations (SAFE)
AbsolutePath absolutePath = TaskEnvironment.GetAbsolutePath(inputPath);
if (File.Exists(absolutePath))
{
    string content = File.ReadAllText(absolutePath);
}
```

`GetAbsolutePath()` throws for null/empty inputs. See [Exception Handling in Batch Operations](#exception-handling-in-batch-operations) for handling strategies.

The [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs) struct:
- Has `Value` property returning the absolute path string
- Has `OriginalValue` property preserving the input path  
- Is implicitly convertible to `string` for File/Directory API compatibility

**CAUTION**: `FileInfo` can be created from relative paths - only use `FileInfo.FullName` if constructed with an absolute path. 

#### Note:
If code previously used `Path.GetFullPath()` for canonicalization (resolving `..` segments, normalizing separators), call `AbsolutePath.GetCanonicalForm()` after absolutization to preserve that behavior. Do not simply replace `Path.GetFullPath` with `GetAbsolutePath` if canonicalization was the intent. You can replace `Path.GetFullPath` behavior by combining both:

```csharp
AbsolutePath absolutePath = TaskEnvironment.GetAbsolutePath(inputPath).GetCanonicalForm();
```
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

## Practical Notes

### CRITICAL: Trace All Path String Usage

**You MUST trace every path string variable through the entire codebase** to find all places where it flows into file system operations - including helper methods, utility classes, and third-party code that may internally use File APIs.

Steps:
1. Find every path string (e.g., `item.ItemSpec`, function parameters)
2. **Trace downstream**: Follow the variable through all method calls and assignments
3. Absolutize BEFORE any code path that touches the file system
4. Use `OriginalValue` for user-facing output (logs, errors)

```csharp
// WRONG - LockCheck internally uses File APIs with non-absolutized path
string sourceSpec = item.ItemSpec;  // sourceSpec is string
string lockedMsg = LockCheck.GetLockedFileMessage(sourceSpec);  // BUG! Trace the call!

// CORRECT - absolutized path passed to helper
AbsolutePath sourceFile = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
string lockedMsg = LockCheck.GetLockedFileMessage(sourceFile);

// For error messages, preserve original user input
Log.LogError("...", sourceFile.OriginalValue, ...);
```

### Exception Handling in Batch Operations

**Important**: `GetAbsolutePath()` throws on null/empty inputs. In batch processing scenarios (e.g., iterating over multiple files), an unhandled exception will abort the entire batch. Tasks must catch and handle these exceptions appropriately to avoid cutting short processing of valid items:

```csharp
// WRONG - one bad path aborts entire batch
foreach (ITaskItem item in SourceFiles)
{
    AbsolutePath path = TaskEnvironment.GetAbsolutePath(item.ItemSpec); // throws, batch stops!
    ProcessFile(path);
}

// CORRECT - handle exceptions, continue processing valid items
bool success = true;
foreach (ITaskItem item in SourceFiles)
{
    try
    {
        AbsolutePath path = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
        ProcessFile(path);
    }
    catch (ArgumentException ex)
    {
        Log.LogError($"Invalid path '{item.ItemSpec}': {ex.Message}");
        success = false;
        // Continue processing remaining items
    }
}
return success;
```

Consider the task's error semantics: should one invalid path fail the entire task immediately, or should all items be processed with errors collected? Match the original task's behavior.

### Prefer AbsolutePath Over String

When working with paths, stay in the `AbsolutePath` world as much as possible rather than converting back and forth to `string`. This reduces unnecessary conversions and maintains type safety:

```csharp
// AVOID - unnecessary conversions
string path = TaskEnvironment.GetAbsolutePath(input).Value;
AbsolutePath again = TaskEnvironment.GetAbsolutePath(path); // redundant!

// PREFER - stay in AbsolutePath
AbsolutePath path = TaskEnvironment.GetAbsolutePath(input);
// Use path directly - it's implicitly convertible to string where needed
File.ReadAllText(path);
```

### TaskEnvironment is Not Thread-Safe

If your task spawns multiple threads internally, you must synchronize access to `TaskEnvironment`. However, each task instance gets its own environment, so no synchronization with other tasks is needed.

## Checklist

- [ ] Task is annotated with `MSBuildMultiThreadableTask` attribute and implements `IMultiThreadableTask` if TaskEnvironment APIs are required
- [ ] All environment variable access uses `TaskEnvironment` APIs
- [ ] All process spawning uses `TaskEnvironment.GetProcessStartInfo()`
- [ ] All file system APIs receive absolute paths
- [ ] All helper methods receiving path strings are traced to verify they don't internally use File APIs with non-absolutized paths
- [ ] No use of `Environment.CurrentDirectory`
- [ ] All tests set `TaskEnvironment = TaskEnvironmentHelper.CreateForTest()`
- [ ] Tests verify exception behavior for null/empty paths
- [ ] No use of forbidden APIs (Environment.Exit, etc.)

## References

- [Thread-Safe Tasks Spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md) - Full specification for multithreaded task support
- [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs) - Struct for representing absolute paths
- [`TaskEnvironment`](https://github.com/dotnet/msbuild/blob/main/src/Framework/TaskEnvironment.cs) - Thread-safe environment APIs for tasks
- [`IMultiThreadableTask`](https://github.com/dotnet/msbuild/blob/main/src/Framework/IMultiThreadableTask.cs) - Interface for multithreaded task support
