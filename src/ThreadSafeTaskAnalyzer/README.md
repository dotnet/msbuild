# Thread-Safe Task Analyzer

This analyzer detects the usage of banned APIs within implementations of `IThreadSafeTask`. Thread-safe tasks are designed to run in parallel during MSBuild execution and must avoid APIs that depend on process-global state to prevent race conditions and non-deterministic behavior.

## Banned API Categories

The analyzer flags the following categories of APIs:

### File System APIs with Current Working Directory Dependencies

- `System.IO.File` - All methods (uses current working directory for relative paths)
- `System.IO.Directory` - All methods (uses current working directory for relative paths)
- `System.IO.Path.GetFullPath(string)` - Uses current working directory
- File stream constructors with string paths
- `FileInfo` and `DirectoryInfo` constructors with relative paths

### Process-Level State Modification

- `System.Environment.CurrentDirectory` - Process-wide current directory
- `System.Environment.SetEnvironmentVariable` - Process-wide environment variables
- `System.Environment.Exit` and `System.Environment.FailFast` - Process termination
- `System.Threading.ThreadPool` min/max thread configuration
- `System.Globalization.CultureInfo` current culture properties

### Process Control

- `System.Diagnostics.Process.Kill` - Process termination
- `System.Diagnostics.Process.Start` overloads that inherit environment

### Assembly Loading

- Various `System.Reflection.Assembly.Load*` methods that may cause version conflicts

## Rule: MSB4260

**Category:** Usage  
**Severity:** Warning  
**Description:** Symbol is banned in IThreadSafeTask implementations

This rule is triggered when code within a class that implements `IThreadSafeTask` uses any of the banned APIs listed above.

## How to Fix Violations

### Use MSBuild APIs Instead

- Use `TaskLoggingHelper` for logging instead of writing to files directly
- Use MSBuild's path utilities for file operations
- Use absolute paths when file operations are necessary

### Use Thread-Local State

- Use `Thread.CurrentThread.CurrentCulture` instead of `CultureInfo.CurrentCulture`
- Pass required configuration through task properties
- Use task-specific working directories passed as absolute paths

## Example

**Bad:**

```csharp
public class MyThreadSafeTask : Task, IThreadSafeTask
{
    public override bool Execute()
    {
        // This will trigger MSB4260
        File.WriteAllText("output.txt", "Hello World");
        return true;
    }
}
```

**Good:**

```csharp
public class MyThreadSafeTask : Task, IThreadSafeTask
{
    [Required]
    public string OutputFile { get; set; }

    public override bool Execute()
    {
        // Use absolute path passed as parameter
        File.WriteAllText(Path.GetFullPath(OutputFile), "Hello World");
        return true;
    }
}
```

## Configuration

The banned APIs are configured in `IThreadSafeTask_BannedApis.txt` using documentation comment IDs. Each line contains:

```text
DocumentationCommentId;Optional descriptive message
```

This analyzer is automatically included when you reference the `Microsoft.Build.Utilities.Core` package.
