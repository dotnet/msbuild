# Thread Safe Tasks

## Overview

In the traditional MSBuild execution model, tasks operate under the assumption that they have exclusive control over the entire process during execution. This allows them to freely modify global process state, including environment variables, the current working directory, and other process-level resources. This design was well-suited for MSBuild's historical approach of using separate processes for parallel execution.

However, with the introduction of multithreaded MSBuild execution mode, multiple tasks can now run concurrently within the same process. This change requires a new approach to task design to prevent race conditions and ensure thread safety. To enable tasks to opt into this multithreaded execution model, we introduce a new interface that tasks should implement and utilize to declare their thread-safety.

Tasks that implement the following `IThreadSafeTask` interface should avoid using APIs that modify global process state or rely on process-level state that could cause conflicts when multiple tasks execute simultaneously. For a list of such APIs and their safe alternatives, refer to [Thread-Safe Tasks API Reference](thread-safe-tasks-api-reference.md).

## IThreadSafeTask Interface

```csharp
/// <summary>
/// Interface for tasks that support multithreaded execution in MSBuild.
/// Tasks implementing this interface guarantee that they can run concurrently with other tasks
/// within the same MSBuild process.
/// </summary>
public interface IThreadSafeTask : ITask
{
    /// <summary>
    /// Execution context for the task, providing thread-safe
    /// access to environment variables, working directory, and other build context.
    /// This property will be set by the MSBuild engine before execution is called.
    /// </summary>
    ITaskExecutionContext ExecutionContext { get; set; }
}
```

### Questions and Design Notes

**TODO**: I want to prevent customers from setting or modifying the ExecutionContext, but I don't want to create it during task construction.

## ITaskExecutionContext Interface

The `ITaskExecutionContext` provides tasks with access to execution environment information that was previously accessed through global process state:

```csharp
/// <summary>
/// Provides access to task execution context and environment information.
/// </summary>
public interface ITaskExecutionContext
{    
    string CurrentDirectory { get; set; }

    IEnvironment Environment { get; }

    IFileSystem FileSystem { get; }
}
```

### Questions and Notes:
1. Should we consider using classes?

## IEnvironment Interface

The `IEnvironment` provides thread-safe access to environment variables:

```csharp
public interface IEnvironment
{
    string? GetEnvironmentVariable(string name);
    
    Dictionary<string, string?> GetEnvironmentVariables();
    
    void SetEnvironmentVariable(string name, string? value);
}
```

## ITaskContextFileSystem Interface

```csharp
/// <summary>
/// Context-aware File System. All Path/File/Directory calls should be used through it.
/// Automatically uses the current working directory from the execution context.
/// </summary>
public interface IFileSystem
{
    IPath Path { get; }
    
    IFile File { get; }
    
    IDirectory Directory { get; }
}
```

### Questions and Notes:
1. Should we flatten the interface? Avoid IFileSystem and place them in the ITaskExecutionContext, and/or remove IPath, IFile, IDirectory? 
1. Should we consider using classes?
1. What will we do if we need to add functions to the interface?

## IPath Interface

Thread-safe alternative to `System.IO.Path` class:

```csharp
public interface IPath
{
    string GetFullPath(string path);
}
```

## IFile Interface

Thread-safe alternative to `System.IO.File` class:

**TODO** Generated with copilot, look that it correctly mirrors all the functions in .NET class that use relative paths.

```csharp
public interface IFile
{
    bool Exists(string path);
    
    string ReadAllText(string path);
    
    string ReadAllText(string path, Encoding encoding);
    
    byte[] ReadAllBytes(string path);
    
    string[] ReadAllLines(string path);
    
    string[] ReadAllLines(string path, Encoding encoding);
    
    IEnumerable<string> ReadLines(string path);
    
    IEnumerable<string> ReadLines(string path, Encoding encoding);
    
    void WriteAllText(string path, string contents);
    
    void WriteAllText(string path, string contents, Encoding encoding);
    
    void WriteAllBytes(string path, byte[] bytes);
    
    void WriteAllLines(string path, string[] contents);
    
    void WriteAllLines(string path, IEnumerable<string> contents);
    
    void WriteAllLines(string path, string[] contents, Encoding encoding);
    
    void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding);
    
    void AppendAllText(string path, string contents);
    
    void AppendAllText(string path, string contents, Encoding encoding);
    
    void AppendAllLines(string path, IEnumerable<string> contents);
    
    void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding);
    
    void Copy(string sourceFileName, string destFileName);

    void Copy(string sourceFileName, string destFileName, bool overwrite);
    
    void Move(string sourceFileName, string destFileName);
    
    void Move(string sourceFileName, string destFileName, bool overwrite);
    
    void Delete(string path);
    
    FileAttributes GetAttributes(string path);
    
    void SetAttributes(string path, FileAttributes fileAttributes);
    
    DateTime GetCreationTime(string path);
    
    DateTime GetCreationTimeUtc(string path);
    
    DateTime GetLastAccessTime(string path);
    
    DateTime GetLastAccessTimeUtc(string path);
    
    DateTime GetLastWriteTime(string path);
    
    DateTime GetLastWriteTimeUtc(string path);
    
    void SetCreationTime(string path, DateTime creationTime);

    void SetCreationTimeUtc(string path, DateTime creationTimeUtc);
    
    void SetLastAccessTime(string path, DateTime lastAccessTime);
    
    void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
    
    void SetLastWriteTime(string path, DateTime lastWriteTime);
    
    void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);
    
    FileStream OpenRead(string path);
    
    FileStream OpenWrite(string path);
    
    FileStream Open(string path, FileMode mode);
    
    FileStream Open(string path, FileMode mode, FileAccess access);
    
    FileStream Open(string path, FileMode mode, FileAccess access, FileShare share);
    
    FileStream Create(string path);
    
    FileStream Create(string path, int bufferSize);
    
    FileStream Create(string path, int bufferSize, FileOptions options);
    
    StreamReader OpenText(string path);
    
    StreamWriter CreateText(string path);
    
    StreamWriter AppendText(string path);
    
    void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName);
    
    void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);
}
```

## IDirectory Interface

Thread-safe alternative to `System.IO.Directory` class:

**TODO** Generated with Copilot, look that it correctly mirrors all the functions in .NET class that use relative paths.

```csharp
public interface IDirectory
{
    bool Exists(string path);
    
    DirectoryInfo CreateDirectory(string path);
    
    void Delete(string path);
    
    void Delete(string path, bool recursive);
    
    void Move(string sourceDirName, string destDirName);
    
    string[] GetFiles(string path);
    
    string[] GetFiles(string path, string searchPattern);
    
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    
    string[] GetDirectories(string path);
    
    string[] GetDirectories(string path, string searchPattern);
    
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
    
    string[] GetFileSystemEntries(string path);
    
    string[] GetFileSystemEntries(string path, string searchPattern);
    
    string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateFiles(string path);
    
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateDirectories(string path);
    
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern);
    
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern);
    
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
    
    DateTime GetCreationTime(string path);
    
    DateTime GetCreationTimeUtc(string path);
    
    DateTime GetLastAccessTime(string path);
    
    DateTime GetLastAccessTimeUtc(string path);
    
    DateTime GetLastWriteTime(string path);
    
    DateTime GetLastWriteTimeUtc(string path);
    
    void SetCreationTime(string path, DateTime creationTime);
    
    void SetCreationTimeUtc(string path, DateTime creationTimeUtc);
    
    void SetLastAccessTime(string path, DateTime lastAccessTime);
    
    void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
    
    void SetLastWriteTime(string path, DateTime lastWriteTime);
    
    void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);
    
    DirectoryInfo GetParent(string path);
    
    string GetDirectoryRoot(string path);
    
    string GetCurrentDirectory();
    
    void SetCurrentDirectory(string path);
}
```

## Notes

**Note**: Our classes should be thread-safe so that task authors can create multi-threaded tasks, and the class should be safe to use.
