---
name: multithreaded-task-migration
description: Guide for migrating MSBuild tasks to multithreaded mode support, including compatibility red-team review. Use this when converting tasks to thread-safe versions, implementing IMultiThreadableTask, adding TaskEnvironment support, or auditing migrations for behavioral compatibility.
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
- [ ] Compatibility red-team review completed (see below)

## References

- [Thread-Safe Tasks Spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md) - Full specification for multithreaded task support
- [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs) - Struct for representing absolute paths
- [`TaskEnvironment`](https://github.com/dotnet/msbuild/blob/main/src/Framework/TaskEnvironment.cs) - Thread-safe environment APIs for tasks
- [`IMultiThreadableTask`](https://github.com/dotnet/msbuild/blob/main/src/Framework/IMultiThreadableTask.cs) - Interface for multithreaded task support

---

# Compatibility Red-Team Playbook

You are an adversarial compatibility engineer. Your job is to find every observable behavioral difference between old code and new code, no matter how subtle. You must assume every difference is a bug until proven otherwise.

## Core Principle

**Observable behavior includes EVERYTHING the build system or user can detect:**
- Return values of `Execute()` (true/false)
- Output properties and their exact string values
- Error/warning messages and their content (paths, formatting)
- Exception types and when they're thrown
- Side effects: files created, file content, file encoding
- Which code path runs (success vs. error handler)
- Ordering of operations (log before file write? file write before output set?)

## The 7 Deadly Compatibility Sins

These are real bugs found during MSBuild task migrations. Every one shipped in initial "passing" code with green tests.

### Sin 1: Output Property Contamination

**The bug**: After absolutizing internal paths, the absolutized value leaks into an output property that users/other tasks consume.

```csharp
// BEFORE: OutputPath was "bin\Release\app.manifest" (relative)
ManifestPath = Path.Combine(OutputDirectory, manifestName);

// BROKEN: OutputPath is now "C:\repo\bin\Release\app.manifest" (absolute!)
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(Path.Combine(OutputDirectory, manifestName));
ManifestPath = abs; // implicit string conversion leaks absolute path!
```

**The fix**: Compute the original-form string for the output property, use the absolutized path only for file I/O.

```csharp
string originalPath = Path.Combine(OutputDirectory, manifestName);
AbsolutePath outputPath = TaskEnvironment.GetAbsolutePath(originalPath);
ManifestPath = originalPath;          // output property: original form
document.Save((string)outputPath);    // file I/O: absolute path
```

**How to detect**: For every `[Output]` property, trace backward — is its value ever assigned from an `AbsolutePath`? If yes, that's a bug.

### Sin 2: Error Message Path Inflation

**The bug**: Error messages now show absolutized paths instead of the user's original input, changing diagnostic output that users, scripts, and CI systems pattern-match on.

```csharp
// BEFORE: "Cannot find manifest 'app.manifest'"
Log.LogError("Cannot find manifest '{0}'", manifestPath);

// BROKEN: "Cannot find manifest 'C:\repo\app.manifest'"
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(manifestPath);
Log.LogError("Cannot find manifest '{0}'", abs); // abs implicitly converts to absolute string!
```

**The fix**: Use `OriginalValue` or stash the original string before absolutizing.

```csharp
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
// File ops use abs, error messages use OriginalValue
Log.LogError("Cannot find manifest '{0}'", abs.OriginalValue);
```

**How to detect**: Search for every `Log.LogError`, `Log.LogWarning`, `Log.LogMessage` call. Is any argument an `AbsolutePath` or derived from one? If yes, verify the pre-migration code used the raw ItemSpec/path string.

### Sin 3: Null Coalescing That Changes Control Flow

**The bug**: Adding defensive `?? ""` or `?? string.Empty` to a nullable return value silently swallows an exception that the old code relied on for error handling.

```csharp
// BEFORE: Path.GetDirectoryName("C:\") returns null
// Then: Path.Combine(null, "file.cs") throws ArgumentNullException
// Then: caught by IsIoRelatedException handler → logs error → returns false
string dir = Path.GetDirectoryName(fileName);
string combined = Path.Combine(dir, dependentUpon); // throws if dir is null!

// BROKEN: Added ?? "" "for safety"
string dir = Path.GetDirectoryName(fileName) ?? string.Empty;
string combined = Path.Combine(dir, dependentUpon); // silently produces "file.cs"
// No exception → no error logged → Execute() returns TRUE instead of FALSE!
```

**The fix**: Don't add null guards that weren't there before. If the old code threw on null, the new code must throw on null too.

**How to detect**: Search for every `??` you added. For each one, ask: "What happened in the old code when this was null?" If the answer is "it threw and was caught", your `??` is a bug.

### Sin 4: Absolutization Before Try-Catch Changes Exception Scope

**The bug**: Moving `GetAbsolutePath()` inside a try-catch that wasn't designed for it can catch the wrong exception type, or moving it outside can leave helper calls without the absolutized value.

```csharp
// BEFORE:
try {
    WriteFile(OutputManifest.ItemSpec);
} catch (Exception ex) {
    string lockMsg = LockCheck.GetLockedFileMessage(OutputManifest.ItemSpec);
    // ↑ LockCheck uses File APIs internally — gets wrong file if CWD differs!
}

// STILL BROKEN (common mistake):
try {
    AbsolutePath abs = TaskEnvironment.GetAbsolutePath(OutputManifest.ItemSpec);
    WriteFile(abs);
} catch (Exception ex) {
    // abs is out of scope here! Falls back to ItemSpec → same bug as before
    string lockMsg = LockCheck.GetLockedFileMessage(OutputManifest.ItemSpec);
}

// CORRECT:
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(OutputManifest.ItemSpec);
try {
    WriteFile(abs);
} catch (Exception ex) {
    string lockMsg = LockCheck.GetLockedFileMessage(abs);        // absolute path
    Log.LogError("Failed: {0}", OutputManifest.ItemSpec, ...);   // original path for message
}
```

**How to detect**: For every `GetAbsolutePath` inside a try block, check what the catch block uses. If catch needs the absolutized path, hoist the call above the try.

### Sin 5: Dictionary Key Mismatch After Absolutization

**The bug**: When paths are used as dictionary keys for deduplication/lookup, absolutizing without canonicalizing creates mismatches — `foo\..\bar` ≠ `bar` even though they're the same file.

```csharp
// BEFORE: Path.GetFullPath canonicalized paths (resolved .., normalized /)
var map = items.ToDictionary(p => Path.GetFullPath(p.ItemSpec), ...);

// BROKEN: GetAbsolutePath does NOT canonicalize
var map = items.ToDictionary(p => TaskEnvironment.GetAbsolutePath(p.ItemSpec), ...);
// "C:\repo\foo\..\bar.dll" and "C:\repo\bar.dll" are now different keys!

// CORRECT: Canonicalize after absolutizing
var map = items.ToDictionary(
    p => (string)TaskEnvironment.GetAbsolutePath(p.ItemSpec).GetCanonicalForm(),
    StringComparer.OrdinalIgnoreCase);
```

**How to detect**: Find every `Dictionary`, `HashSet`, `ToDictionary`, `.ContainsKey`, `.TryGetValue` that uses paths as keys. Was the old code using `Path.GetFullPath`? If yes, the new code must also canonicalize.

### Sin 6: Exception Type Change Breaking Catch Filters

**The bug**: Old code threw `FileNotFoundException` for a missing file; new code throws `ArgumentException` from `GetAbsolutePath("")` before even reaching the file check. Different exception type may bypass different catch handlers.

```csharp
// BEFORE: Empty path → File.Exists("") returns false → FileNotFoundException
// Or: Empty path → Path.GetFullPath("") throws ArgumentException

// AFTER: Empty path → GetAbsolutePath("") throws ArgumentException immediately
// If the old code had specific FileNotFoundException handling, it's now bypassed
```

**How to detect**: For every `GetAbsolutePath` call, ask: "What exception did the old code throw for empty/null input?" Then check if the calling code has catch blocks that filter by exception type. `ExceptionHandling.IsIoRelatedException` catches `ArgumentException`, but custom catch blocks might not.

### Sin 7: Path.GetFullPath Side Effects You Didn't Know About

`Path.GetFullPath(relative)` does TWO things:
1. Resolves against CWD to make absolute
2. Canonicalizes: resolves `..`, normalizes `\` vs `/`, removes trailing separators

`TaskEnvironment.GetAbsolutePath(relative)` does only #1 (via `Path.Combine`). If the old code depended on canonicalization (e.g., for display, comparison, or as dictionary keys), you must add `.GetCanonicalForm()` after absolutizing.

```csharp
// Path.GetFullPath("foo/../bar.dll")  → "C:\repo\bar.dll"       (canonical)
// GetAbsolutePath("foo/../bar.dll")   → "C:\repo\foo/../bar.dll" (NOT canonical)
```

## Red-Team Audit Protocol

### Phase 1: Trace Every Changed Line

For each modified line:
1. What was the **exact** runtime value before the change?
2. What is the **exact** runtime value after the change?
3. Where does this value flow to? (outputs, logs, file paths, comparisons, dictionary keys)
4. For EACH destination: does the changed value produce identical observable behavior?

### Phase 2: Null/Empty/Edge Input Analysis

For every `GetAbsolutePath` call, test these inputs:
| Input | `GetAbsolutePath` behavior | Old behavior | Match? |
|---|---|---|---|
| `null` | `ArgumentException` | Varies | ❓ |
| `""` | `ArgumentException` | Varies | ❓ |
| `"C:\"` (root) | Valid absolute | Valid absolute | ✅ usually |
| `"."` | `"C:\repo\."` (not canonical) | `"C:\repo"` if GetFullPath | ❌ maybe |
| `"foo\..\bar"` | `"C:\repo\foo\..\bar"` (not canonical) | `"C:\repo\bar"` if GetFullPath | ❌ maybe |
| Already absolute `"C:\foo"` | `"C:\foo"` | `"C:\foo"` | ✅ |

For every `Path.GetDirectoryName` → `Path.Combine` chain:
| Input to GetDirectoryName | Returns | Path.Combine(result, x) | Throws? |
|---|---|---|---|
| `"C:\"` | `null` | `ArgumentNullException` | ✅ if old threw |
| `"C:\foo"` | `"C:\"` | Works | ✅ |
| `""` | `""` (.NET Fx) / `null` (.NET Core) | Works / Throws | ⚠️ |
| `"foo.resx"` (no dir) | `""` | Works | ✅ |

### Phase 3: Cross-Framework Divergence

These APIs behave differently on .NET Framework vs .NET Core+:
- `Path.GetDirectoryName("")`: `""` on Framework, exception or `null` on Core
- `Path.GetFullPath` with URIs: may work on Framework, throws on Core
- `Path.IsPathFullyQualified`: .NET Core+ only; Framework may not have it
- `Path.Combine` with null: `ArgumentNullException` on both, but exception message text differs

For every changed code path, verify behavior on BOTH `net472` and `net10.0` targets.

### Phase 4: Downstream Impact Tracing

Don't just look at the changed file. Trace the data flow:
1. **Output properties**: What reads this task's `[Output]`? Does the consumer compare it, use it as a path, display it?
2. **Written files**: Does the file content change? (e.g., manifest XML with paths inside)
3. **Helper methods**: Does `LockCheck`, `ManifestWriter`, `FileSystems.Default` internally resolve relative paths? If so, they need absolutized input.
4. **Logged messages**: Are error codes preserved? MSBuild error codes (MSBxxxx) must be identical.

### Phase 5: Concurrency Stress

The whole point of the migration is thread safety. Verify:
1. Two tasks with different `TaskEnvironment.ProjectDirectory` values don't interfere
2. Static fields aren't written to (they're shared across threads)
3. File operations use absolutized paths (CWD is meaningless in multi-threaded mode)

## Compatibility Test Generation Template

For each changed task, generate tests in this matrix:

```
[Task] × [Input Type] × [Assertion Category]

Input Types:
  - Happy path (normal relative path)
  - Already absolute path
  - Null/empty path
  - Path with .. segments
  - Root path ("C:\")
  - Path with forward slashes
  - Path with trailing separator
  - UNC path ("\\server\share\file")
  - Very long path (260+ chars)

Assertion Categories:
  - Execute() return value (true/false)
  - Output property exact string value
  - Error message content (path shown to user)
  - Exception type thrown
  - File written to correct location
  - File content matches expected
```

### Minimal Compatibility Test Pattern

```csharp
[Fact]
public void OutputProperty_PreservesOriginalForm()
{
    // Arrange: set up task with RELATIVE path input
    var task = CreateTaskWithRelativeInput("subdir\\file.manifest");
    
    // Act
    task.Execute();
    
    // Assert: output property must NOT be absolutized
    task.OutputPath.ShouldNotStartWith("C:\\");
    task.OutputPath.ShouldBe("subdir\\file.manifest");
}

[Fact]
public void ErrorMessage_ShowsOriginalPath_NotAbsolute()
{
    // Arrange: set up task with input that will cause error
    var task = CreateTaskWithMissingInput("missing\\file.xml");
    
    // Act
    task.Execute();
    
    // Assert: error message uses original user path
    var errors = ((MockEngine)task.BuildEngine).Errors;
    errors.ShouldContain(e => e.Contains("missing\\file.xml"));
    errors.ShouldNotContain(e => e.Contains(Directory.GetCurrentDirectory()));
}

[Fact]
public void NullInput_SameExceptionType_AsBefore()
{
    // Arrange
    var task = CreateTaskWithNullInput();
    
    // Act & Assert: must throw same type as pre-migration
    Should.Throw<ArgumentNullException>(() => task.Execute());
    // NOT ArgumentException, NOT NullReferenceException
}
```

## Compatibility Sign-Off Checklist

Before approving a migration as compatible:

- [ ] Every `[Output]` property verified: exact string value matches pre-migration
- [ ] Every `Log.LogError`/`LogWarning` call verified: path in message matches pre-migration
- [ ] Every `GetAbsolutePath` call: null/empty behavior matches old exception path
- [ ] Every dictionary/set using paths as keys: canonicalization preserved (use `GetCanonicalForm()`)
- [ ] Every try-catch: absolutized value available in catch block where needed
- [ ] Every `??` or `?.` added: verified it doesn't swallow a previously-thrown exception
- [ ] Cross-framework: tested on both net472 and net10.0
- [ ] No `AbsolutePath` value leaks into user-visible strings without using `OriginalValue`
- [ ] Helper methods receiving paths traced to verify they don't internally use File APIs with non-absolutized paths
- [ ] Concurrent execution: two tasks with different project directories produce correct independent results
