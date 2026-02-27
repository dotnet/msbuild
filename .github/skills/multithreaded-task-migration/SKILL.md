---
name: multithreaded-task-migration
description: Guide for migrating MSBuild tasks to multithreaded mode support, including compatibility red-team review. Use this when converting tasks to thread-safe versions, implementing IMultiThreadableTask, adding TaskEnvironment support, or auditing migrations for behavioral compatibility.
---

# Migrating MSBuild Tasks to Multithreaded API

MSBuild's multithreaded execution model requires tasks to avoid global process state (working directory, environment variables). Thread-safe tasks declare this capability via `MSBuildMultiThreadableTask` and use `TaskEnvironment` from `IMultiThreadableTask` for safe alternatives.

## Migration Steps

### Step 1: Update Task Class Declaration

a. Ensure the task implementing class is decorated with the `MSBuildMultiThreadableTask` attribute.
b. Implement `IMultiThreadableTask` **only if** the task needs `TaskEnvironment` APIs (path absolutization, env vars, process start). If the task has no file/environment operations (e.g., a stub class), the attribute alone is sufficient.

```csharp
[MSBuildMultiThreadableTask]
public class MyTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    ...
}
```

**Note**: `[MSBuildMultiThreadableTask]` has `Inherited = false` — it must be on each concrete class, not just the base.

### Step 2: Absolutize Paths Before File Operations

All path strings must be absolutized with `TaskEnvironment.GetAbsolutePath()` before use in file system APIs. This resolves paths relative to the project directory, not the process working directory.

```csharp
AbsolutePath absolutePath = TaskEnvironment.GetAbsolutePath(inputPath);
if (File.Exists(absolutePath))
{
    string content = File.ReadAllText(absolutePath);
}
```

The [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs) struct:
- `Value` — the absolute path string
- `OriginalValue` — preserves the input path (use for error messages and `[Output]` properties)
- Implicitly convertible to `string` for File/Directory API compatibility
- `GetCanonicalForm()` — resolves `..` segments and normalizes separators (see [Sin 5](#sin-5-canonicalization-mismatch))

**CAUTION**: `GetAbsolutePath()` throws `ArgumentException` for null/empty inputs. See [Sin 3](#sin-3-null-coalescing-that-changes-control-flow) and [Sin 6](#sin-6-exception-type-change) for compatibility implications.

### Step 3: Replace Environment Variable APIs

| BEFORE (UNSAFE)                                  | AFTER (SAFE)                                       |
|--------------------------------------------------|----------------------------------------------------|
| `Environment.GetEnvironmentVariable("VAR");`       | `TaskEnvironment.GetEnvironmentVariable("VAR");`     |
| `Environment.SetEnvironmentVariable("VAR", "v");`  | `TaskEnvironment.SetEnvironmentVariable("VAR", "v");` |

### Step 4: Replace Process Start APIs

| BEFORE (UNSAFE - inherits process state)           | AFTER (SAFE - uses task's isolated environment)     |
|----------------------------------------------------|-----------------------------------------------------|
| `var psi = new ProcessStartInfo("tool.exe");`      | `var psi = TaskEnvironment.GetProcessStartInfo();`  |
|                                                    | `psi.FileName = "tool.exe";`                        |

## Updating Unit Tests

Every test creating a task instance must set `TaskEnvironment = TaskEnvironmentHelper.CreateForTest()`.

## APIs to Avoid

| Category | APIs | Alternative |
|---|---|---|
| **Forbidden** | `Environment.Exit`, `FailFast`, `Process.Kill`, `ThreadPool.SetMin/MaxThreads`, `Console.*` | Return false, throw, or use `Log` |
| **Use TaskEnvironment** | `Environment.CurrentDirectory`, `Get/SetEnvironmentVariable`, `Path.GetFullPath`, `ProcessStartInfo` | See Steps 2-4 |
| **Need absolute paths** | `File.*`, `Directory.*`, `FileInfo`, `DirectoryInfo`, `FileStream`, `StreamReader/Writer` | Absolutize first (File System APIs) |
| **Review required** | `Assembly.Load*`, `Activator.CreateInstance*` | Check for version conflicts |

## Practical Notes

### CRITICAL: Trace All Path String Usage

Trace every path string through all method calls and assignments to find all places it flows into file system operations — including helper methods that may internally use File System APIs.

1. Find every path string (e.g., `item.ItemSpec`, function parameters)
2. Trace downstream through all method calls
3. Absolutize BEFORE any code path that touches the file system
4. Use `OriginalValue` for user-facing output (logs, errors) — see [Sin 2](#sin-2-error-message-path-inflation)

### Exception Handling in Batch Operations

In batch processing (iterating over files), `GetAbsolutePath()` throwing on one bad path aborts the entire batch. Match the original task's error semantics:

```csharp
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
        Log.LogError("Invalid path '{0}': {1}", item.ItemSpec, ex.Message);
        success = false;
    }
}
return success;
```

### Prefer AbsolutePath Over String

Stay in the `AbsolutePath` world — it's implicitly convertible to `string` where needed. Avoid round-tripping through `string` and back.

### TaskEnvironment is Not Thread-Safe

If your task spawns multiple threads internally, synchronize access to `TaskEnvironment`. Each task *instance* gets its own environment, so no synchronization between tasks is needed.

## References

- [Thread-Safe Tasks Spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md)
- [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs)
- [`TaskEnvironment`](https://github.com/dotnet/msbuild/blob/main/src/Framework/TaskEnvironment.cs)
- [`IMultiThreadableTask`](https://github.com/dotnet/msbuild/blob/main/src/Framework/IMultiThreadableTask.cs)

---

# Compatibility Red-Team Playbook

After migration, review for behavioral compatibility. **Every observable difference is a bug until proven otherwise.**

Observable behavior = `Execute()` return value, `[Output]` property values, error/warning message content, exception types, files written, and which code path runs.

## The 6 Deadly Compatibility Sins

Real bugs found during MSBuild task migrations. Every one shipped in initial "passing" code with green tests.

### Sin 1: Output Property Contamination

Absolutized values leak into `[Output]` properties that users/other tasks consume.

```csharp
// BROKEN: ManifestPath was "bin\Release\app.manifest", now "C:\repo\bin\Release\app.manifest"
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(Path.Combine(OutputDirectory, name));
ManifestPath = abs; // implicit string conversion!

// CORRECT: separate original form from absolutized path
string originalPath = Path.Combine(OutputDirectory, name);
AbsolutePath outputPath = TaskEnvironment.GetAbsolutePath(originalPath);
ManifestPath = originalPath;          // [Output]: original form
document.Save((string)outputPath);    // file I/O: absolute path
```

**Detect**: For every `[Output]` property, trace backward — is it ever assigned from an `AbsolutePath`?

### Sin 2: Error Message Path Inflation

Error messages show absolutized paths instead of the user's original input.

```csharp
// BROKEN: "Cannot find 'C:\repo\app.manifest'" instead of "Cannot find 'app.manifest'"
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(path);
Log.LogError("Cannot find '{0}'", abs); // implicit conversion!

// CORRECT: use OriginalValue
Log.LogError("Cannot find '{0}'", abs.OriginalValue);
```

**Detect**: Search every `Log.LogError`/`LogWarning`/`LogMessage` — is any argument an `AbsolutePath`?

### Sin 3: Null Coalescing That Changes Control Flow

Adding `?? ""` silently swallows an exception the old code relied on for error handling.

```csharp
// BEFORE: Path.GetDirectoryName("C:\") → null → Path.Combine(null, x) → ArgumentNullException
//   → task fails with an exception / error logged → Execute() returns false

// BROKEN: ?? "" added "for safety"
string dir = Path.GetDirectoryName(fileName) ?? string.Empty;
// Path.Combine("", x) succeeds silently → no error → Execute() returns TRUE!
```

**Detect**: For every `??` you added, ask: "What happened when this was null before?" If it threw and was caught → your `??` is a bug.

### Sin 4: Try-Catch Scope Mismatch

`GetAbsolutePath()` inside a try block leaves the absolutized value out of scope in the catch block. Helper methods in the catch (like `LockCheck`) then use the original non-absolute path.

```csharp
// CORRECT: hoist above try so catch can use it too
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(OutputManifest.ItemSpec);
try {
    WriteFile(abs);
} catch (Exception ex) {
    string lockMsg = LockCheck.GetLockedFileMessage(abs);        // absolute → correct file
    Log.LogError("Failed: {0}", OutputManifest.ItemSpec, ...);   // original → user-friendly
}
```

**Detect**: For every `GetAbsolutePath` inside a try, check if the catch block needs the absolutized value.

### Sin 5: Canonicalization Mismatch

`GetAbsolutePath` does NOT canonicalize. `Path.GetFullPath` does TWO things: absolutize AND canonicalize (`..` resolution, separator normalization). If the old code used `Path.GetFullPath` for dictionary keys, comparisons, or display, you must add `.GetCanonicalForm()`:

```csharp
// GetAbsolutePath("foo/../bar")  → "C:\repo\foo/../bar"  (NOT canonical)
// Path.GetFullPath("foo/../bar") → "C:\repo\bar"         (canonical)

// BROKEN for dictionary keys — "C:\repo\foo\..\bar" ≠ "C:\repo\bar"
var map = items.ToDictionary(p => (string)TaskEnvironment.GetAbsolutePath(p.ItemSpec), ...);

// CORRECT
var map = items.ToDictionary(
    p => (string)TaskEnvironment.GetAbsolutePath(p.ItemSpec).GetCanonicalForm(),
    StringComparer.OrdinalIgnoreCase);
```

**Detect**: Find every `Dictionary`/`HashSet`/`ToDictionary` using path keys, and every place the old code called `Path.GetFullPath`. If canonicalization mattered, add `.GetCanonicalForm()`.

### Sin 6: Exception Type Change

Old code threw `FileNotFoundException` for missing files; new code throws `ArgumentException` from `GetAbsolutePath("")` before reaching the file check. Custom catch blocks filtering by exception type may be bypassed. (`ExceptionHandling.IsIoRelatedException` catches `ArgumentException`, but task-specific handlers might not.)

**Detect**: For every `GetAbsolutePath`, check what the old code threw for null/empty and whether the calling code has type-specific catch blocks.

## Red-Team Audit Protocol

### Phase 1: Trace Every Changed Line

For each modified line: What was the exact runtime value before? After? Where does it flow (outputs, logs, file paths, dictionary keys)? Does each destination produce identical observable behavior?

### Phase 2: Null/Empty/Edge Input Analysis

| Input | `GetAbsolutePath` | Old behavior | Match? |
|---|---|---|---|
| `null` | `ArgumentException` | Varies | ❓ |
| `""` | `ArgumentException` | Varies | ❓ |
| `"C:\"` (root) | Valid | Valid | ✅ usually |
| `"."` | `"C:\repo\."` (not canonical) | `"C:\repo"` if GetFullPath | ❌ maybe |
| `"foo\..\bar"` | `"C:\repo\foo\..\bar"` | `"C:\repo\bar"` if GetFullPath | ❌ maybe |
| Already absolute | Pass-through | Pass-through | ✅ |

`Path.GetDirectoryName` → `Path.Combine` chains:
| Input | `GetDirectoryName` returns | `Path.Combine(result, x)` |
|---|---|---|
| `"C:\"` | `null` | Throws `ArgumentNullException` |
| `""` | `""` (.NET Fx) / `null` (.NET Core+) | Works / Throws ⚠️ |
| `"file.resx"` (no dir) | `""` | Works |

Verify behavior on **both** `net472` and `net10.0`.

### Phase 3: Downstream Impact

1. **Output properties**: What consumes this task's `[Output]`? Does it compare, display, or use as a path?
2. **Written files**: Does file content change? (e.g., XML with embedded paths)
3. **Helper methods**: Do `LockCheck`, `ManifestWriter`, etc. internally resolve relative paths?
4. **Error codes**: MSBuild error codes (MSBxxxx) must be identical.

### Phase 4: Concurrency

1. Two tasks with different `ProjectDirectory` values don't interfere
2. No writes to static fields (shared across threads)
3. All file operations use absolutized paths

## Compatibility Test Matrix

```
[Task] × [Input Type] × [Assertion]

Inputs: relative path, absolute path, null, empty, ".." segments, root "C:\",
        forward slashes, trailing separator, UNC path, 260+ char path

Assertions: Execute() return value, [Output] exact string, error message content,
            exception type, file location, file content
```

## Sign-Off Checklist

- [ ] `[MSBuildMultiThreadableTask]` on every concrete class (not just base — `Inherited=false`)
- [ ] `IMultiThreadableTask` only on classes that use `TaskEnvironment` APIs
- [ ] Every `[Output]` property: exact string value matches pre-migration
- [ ] Every `Log.LogError`/`LogWarning`: path in message matches pre-migration (use `OriginalValue`)
- [ ] Every `GetAbsolutePath` call: null/empty exception behavior matches old code path
- [ ] Every dictionary/set with path keys: canonicalization preserved (`GetCanonicalForm()`)
- [ ] Every try-catch: absolutized value available in catch block where needed
- [ ] Every `??` or `?.` added: verified it doesn't swallow a previously-thrown exception
- [ ] No `AbsolutePath` leaks into user-visible strings unintentionally
- [ ] Helper methods traced for internal File API usage with non-absolutized paths
- [ ] All tests set `TaskEnvironment = TaskEnvironmentHelper.CreateForTest()`
- [ ] Cross-framework: tested on both net472 and net10.0
- [ ] Concurrent execution: two tasks with different project directories produce correct results
- [ ] No forbidden APIs (`Environment.Exit`, `Console.*`, etc.)
