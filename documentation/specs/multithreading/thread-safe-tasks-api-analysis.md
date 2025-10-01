# Thread-Safe Tasks: API Analysis Reference (DRAFT)

This document provides a list of .NET APIs that should not be used or should be used with caution in thread-safe tasks. These APIs are problematic because they either rely on or modify process-level state, which can cause race conditions in multithreaded execution.

The APIs listed in this document will be detected by Roslyn analyzers and/or MSBuild BuildCheck to help identify potential threading issues in tasks that implement `IMultiThreadableTask`.

**Note**: The analyzers rely on **static code analysis** and may not catch all dynamic scenarios (such as reflection-based API calls).

## API Issues Categories

Categories of threading issues with .NET API usage in thread-safe tasks to be aware of:

1. **Working Directory Modifications and Usage**, such as file system operations with relative paths.
1. **Environment Variables Modification and Usage**
1. **Process Culture Modification and Usage**, which can affect data formatting.
1. **Assembly Loading**
1. **Static Fields**

### Best Practices

Instead of the problematic APIs listed below, thread-safe tasks should:

1. **Use `TaskEnvironment`** for all file system operations, environment variable changes, and working directory changes.
1. **Always use absolute paths** when still using some standard .NET file system APIs.
1. **Explicitly configure external processes** using `TaskEnvironment`.
1. **Never modify process culture**: Avoid modifying culture defaults.

## Detailed API Reference

The following tables list specific .NET APIs and their threading safety classification:

### System.IO.Path Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| `Path.GetFullPath(string path)` | ERROR | Uses current working directory | Use MSBuild API |

### System.IO.File Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| All methods | ERROR | Uses current working directory | Use absolute paths |

### System.IO.Directory Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| All methods | ERROR | Uses current working directory | Use absolute paths |

### System.Environment Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
|  All properties setters | ERROR | Modifies process-level state | Use MSBuild API |
| `Environment.CurrentDirectory` (getter, setter) | ERROR | Accesses process-level state | Use MSBuild API |
| `Environment.Exit(int exitCode)` | ERROR | Terminates entire process | Return false from task or throw exception |
| `Environment.FailFast` all overloads | ERROR | Terminates entire process | Return false from task or throw exception |
| All other methods | ERROR | Modifies process-level state | Use MSBuild API |

### System.IO.FileInfo Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Constructor `FileInfo(string fileName)` | WARNING | Uses current working directory | Use absolute paths |
| `CopyTo` all overloads | WARNING | Destination path relative to current directory | Use absolute paths |
| `MoveTo` all overloads | WARNING | Destination path relative to current directory | Use absolute paths |
| `Replace` all overloads | WARNING | Paths relative to current directory | Use absolute paths |

### System.IO.DirectoryInfo Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Constructor `DirectoryInfo(string path)` | WARNING | Uses current working directory | Use absolute paths |
| `MoveTo(string destDirName)` | WARNING | Destination path relative to current directory | Use absolute paths |

### System.IO.FileStream Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Constructor `FileStream` all overloads | WARNING | Uses current working directory | Use absolute paths |

### System.IO Stream Classes

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Constructor `StreamReader` all overloads | WARNING | Uses current working directory | Use absolute paths |

### System.Diagnostics.Process Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| All properties setters | ERROR | Modifies process-level state | Avoid |
| `Process.GetCurrentProcess().Kill()` | ERROR | Terminates entire process | Avoid |
| `Process.GetCurrentProcess().Kill(bool entireProcessTree)` | ERROR | Terminates entire process | Avoid |
| `Process.Start` all overloads | ERROR | May inherit process state | Use MSBuild API |

### System.Diagnostics.ProcessStartInfo Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Constructor `ProcessStartInfo()` all overloads | ERROR | May inherit process state | Use MSBuild API |

### System.Threading.ThreadPool Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| `ThreadPool.SetMinThreads(int workerThreads, int completionPortThreads)` | ERROR | Modifies process-wide settings | Avoid |
| `ThreadPool.SetMaxThreads(int workerThreads, int completionPortThreads)` | ERROR | Modifies process-wide settings | Avoid |

### System.Globalization.CultureInfo Class

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| `CultureInfo.DefaultThreadCurrentCulture` (setter) | ERROR | Affects new threads | Modify the thread culture instead |
| `CultureInfo.DefaultThreadCurrentUICulture` (setter) | ERROR | Affects new threads | Modify the thread culture instead |

### Static

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| Static fields | WARNING | Shared across threads, can cause race conditions | Avoid |

### Assembly Loading (System.Reflection.Assembly class, System.Activator class)
Tasks that load assemblies dynamically in the task host may cause version conflicts. Version conflicts in task assemblies will cause build failures (previously these might have been sporadic). Both dynamically loaded dependencies and static dependencies can cause issues.

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| `Assembly.LoadFrom(string assemblyFile)` | WARNING | May cause version conflicts | Be aware of potential conflicts, use absolute paths |
| `Assembly.LoadFile(string path)` | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `Assembly.Load` all overloads | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `Assembly.LoadWithPartialName(string partialName)` | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `Activator.CreateInstanceFrom(string assemblyFile, string typeName)` | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `Activator.CreateInstance(string assemblyName, string typeName)` | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `AppDomain.Load` all overloads | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `AppDomain.CreateInstanceFrom(string assemblyFile, string typeName)` | WARNING | May cause version conflicts | Be aware of potential conflicts |
| `AppDomain.CreateInstance(string assemblyName, string typeName)` | WARNING | May cause version conflicts | Be aware of potential conflicts |

### P/Invoke

**Concerns**:
- P/Invoke calls may use process-level state like current working directory
- Native code may not be thread-safe
- Native APIs may modify global process state

| API | Level | Short Reason | Recommendation |
|-----|-------|--------------|-------|
| `[DllImport]` attribute | WARNING | Not covered by analyzers | Review for thread safety, use absolute paths |