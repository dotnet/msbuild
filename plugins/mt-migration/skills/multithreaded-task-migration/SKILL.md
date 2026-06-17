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
    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
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
|                                                    | `psi.FileName = GetFullPathToTool(); // must be absolute` |

## Updating Unit Tests

Built-in MSBuild tasks now initialize `TaskEnvironment` with a `MultiProcessTaskEnvironmentDriver`-backed default. Tests creating instances of built-in tasks no longer need manual `TaskEnvironment` setup. For custom or third-party tasks that implement `IMultiThreadableTask` without a default initializer, set `TaskEnvironment = TaskEnvironment.Fallback` (or use `TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(path)` to point at a specific project directory).

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

### API Discipline When Plumbing Through Helpers

When the migration ripples into shared helpers:

- **Helper signatures: take `AbsolutePath`, not `(string path, string pathForMessages)`**. Two-string signatures drift apart; the caller passing one `AbsolutePath` keeps `.Value` and `.OriginalValue` in lockstep.
- **Repo-wide helpers belong in an extensions class** (e.g., `TaskEnvironmentExtensions`), not on the task. If a per-task helper looks generic, extract it.
- **Don't condition behavior on MT-mode**. `TaskEnvironment.Fallback` already gives single-process semantics; `if (mtMode) { … } else { … }` doubles the maintenance surface and skips test coverage of one branch.
- **Don't silently swallow `ArgumentException` from `GetAbsolutePath`** in a helper — log a diagnostic so customers can debug bad inputs.

## References

- [Thread-Safe Tasks Spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md)
- [`AbsolutePath`](https://github.com/dotnet/msbuild/blob/main/src/Framework/PathHelpers/AbsolutePath.cs)
- [`TaskEnvironment`](https://github.com/dotnet/msbuild/blob/main/src/Framework/TaskEnvironment.cs)
- [`IMultiThreadableTask`](https://github.com/dotnet/msbuild/blob/main/src/Framework/IMultiThreadableTask.cs)

---

# Compatibility Red-Team Playbook

After migration, review for behavioral compatibility. **Every observable difference is a bug until proven otherwise.**

Observable behavior = `Execute()` return value, `[Output]` property values, error/warning message content, exception types, files written, and which code path runs.

## The 8 Deadly Compatibility Sins

Real bugs found during MSBuild task migrations. Every one shipped in initial "passing" code with green tests.

**Edge-case discipline:** For every migrated code path, verify behavior when inputs are `null`, empty string (`""`), or whitespace-only. `GetAbsolutePath` throws `ArgumentException` on null/empty — if the pre-migration code handled these differently (e.g., returned early, used a default, or threw a different exception type), the migration must preserve that behavior. Even if a scenario seems unlikely, treat it as a relevant finding if it is theoretically possible.

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

### Sin 7: Exception Message Path Leakage

> **How this differs from Sin 2:** Sin 2 is about directly passing an `AbsolutePath` to `Log.LogError`/`LogWarning`. Sin 7 is about *exception messages* that **indirectly** embed the absolute path — the task never logged it, but a helper it called did via `ex.Message`/`ex.FileName`. The fix is also different: Sin 2 uses `OriginalValue`; Sin 7 requires sanitizing the exception string.

Exceptions thrown by *helpers you called with an absolutized path* carry that absolute path in `ex.Message` / `ex.FileName`. Logging the exception verbatim leaks the absolute path even though the task itself never logged an `AbsolutePath` directly.

```csharp
// BROKEN: ex.FileName / ex.Message embed the absolutized path
catch (FileNotFoundException ex) { Log.LogError("Not found: {0}", ex.FileName); }
catch (Exception ex)              { Log.LogError(ex.Message); }

// CORRECT: prefer the original input; if you must use the exception, sanitize
catch (FileNotFoundException ex) { Log.LogError("Not found: {0}", abs.OriginalValue); }
catch (Exception ex)              { Log.LogError(ex.Message.Replace(abs.Value, abs.OriginalValue)); }
```

**Detect**: For every `Log.LogError(ex.Message …)` / `ex.FileName` downstream of a `GetAbsolutePath`, check whether the exception originated from a helper that received the absolutized path. If yes, sanitize.

### Sin 8: `Path.IsPathRooted` as an Absolutize Gate

`Path.IsPathRooted` returns `true` for **drive-relative** (`C:foo\bar`) and **root-relative** (`\foo\bar`) Windows paths — both still depend on process current-directory / current-drive state. "Absolutize only if not rooted" silently leaves those paths process-dependent.

`GetAbsolutePath` correctly handles all path forms — including the Windows edge cases (`C:foo`, `\foo`) that `Path.IsPathRooted` considers "rooted" but that are still CWD/drive-dependent. Call it unconditionally; remove any `IsPathRooted` short-circuit.

---

## Call-Chain Hazards Beyond the Task

The 8 sins above are *local* to the task body. The other half of MT migration is auditing the **transitive call chain** — helpers and shared utility classes the task reaches into.

### Helpers That Capture Process State

Helpers reached from `Execute()` can quietly depend on process state in any of these ways:

- A relative path falls through to `Directory.GetCurrentDirectory()` (often as a "fallback") or to `Path.GetFullPath(x)` without a base.
- An env var is read directly via `Environment.GetEnvironmentVariable` (not `TaskEnvironment`).
- A `static` field is seeded from process state on first use (`static string s_x = Directory.GetCurrentDirectory()`), permanently capturing the first caller's environment for every later caller.
- A BCL API echoes its input path back through `ex.Message` / `ex.FileName` (Sin 7).
- The helper takes a `string path` and returns a `string` — losing the `.OriginalValue` distinction, so callers either lie in messages or absolutize twice.

**Resolution patterns:**

1. Add an MT-aware overload that takes `TaskEnvironment` (or an `AbsolutePath` / explicit fallback path). Legacy overload delegates with `TaskEnvironment.Fallback`. Don't branch on "MT mode on/off".
2. Promote the parameter type to `AbsolutePath` so `.Value` and `.OriginalValue` travel together.
3. Replace process-state-seeded static caches with `ConcurrentDictionary<TKey, TValue>` keyed on the inputs that determine uniqueness — never on process state.
4. For cross-repo helpers (foundation types used by many tasks), migrate the foundation in a dedicated PR before the tasks that consume it; the hydration step is usually where `Path.GetFullPath` is hiding.

### Tasks Instantiated Directly by Other Tasks

`[MSBuildMultiThreadableTask]` is only honored when the TaskFactory creates the task (i.e., a target invokes it as `<MyTask … />`). A task created via `new MyTask()` inside another task's body bypasses the factory and **never gets `TaskEnvironment` injected** — it silently defaults to `TaskEnvironment.Fallback` (= process CWD).

Migrating a nested task in isolation is therefore meaningless. Either migrate the parent and have it explicitly propagate its `TaskEnvironment` to the nested instance before calling `Execute()`, or restructure so the nested task is a regular TaskFactory-created task.

**Detect:** grep for `new …Task()` followed by `.Execute()` — every direct instantiation is a hole.

### ToolTask Override Hot Spots

`ToolTask` subclasses inherit virtual methods that run *before or after* `Execute()` and frequently touch the file system. Audit every override:

| Override | Hazard | Migration |
|---|---|---|
| `GenerateFullPathToTool()` | Builds `ProcessStartInfo.FileName` — relative tool path → wrong tool launched | Absolutize the tool path before returning |
| `SkipTaskExecution()` | Up-to-date check on input/output timestamps using relative paths | Absolutize both sides of the comparison |
| `ValidateParameters()` | `File.Exists` on input parameters | Absolutize before probing |
| `GenerateResponseFileCommands()` / `GenerateCommandLineCommands()` | Tempting to absolutize args | **Don't** — the child's `WorkingDirectory` is the project dir, so relative args resolve correctly; absolutizing inflates user-visible output and can leak into tool diagnostics or generated artifacts |
| `GetWorkingDirectory()` | Default `null` → child inherits host CWD | Leave alone if you use `TaskEnvironment.GetProcessStartInfo()`; it already sets `WorkingDirectory` |

**Key contract:** `ProcessStartInfo.FileName` is resolved by the OS **before** `WorkingDirectory` takes effect, so the executable path must be absolute. Tool arguments are interpreted by the child process in its working directory, so they should stay relative.

### Engine-Owned Environment Variables Are Immutable in MT Mode

Env vars consumed by the engine *before* tasks run (e.g., `MSBUILD*`-prefixed flags and the framework/SDK discovery variables) are snapshotted into engine caches at startup. Tasks that mutate them later have no observable effect on the engine — but the mutation appears to succeed locally, masking bugs.

If a migrated task previously mutated such a variable to influence a downstream tool, re-architect to pass the value via `ProcessStartInfo.EnvironmentVariables` on the child process instead of mutating the parent's environment.

---

## Test Patterns for MT Migrations

A migration test must **fail when the migration is undone**. Tests that pass identically against pre- and post-migration code are theater. The two patterns below are the ones that reliably exercise MT-specific behavior.

### Pattern A: Decoy-CWD Test (for tasks that touch the file system)

Set the process working directory to a **decoy** dir with no relevant files; set `TaskEnvironment.ProjectDirectory` to a different dir with the inputs/expected outputs. The task must read/write against the project directory, not the decoy.

```csharp
using TestEnvironment env = TestEnvironment.Create();
TransientTestFolder projectDir = env.CreateFolder();
TransientTestFolder decoyCwd = env.CreateFolder();
File.WriteAllText(Path.Combine(projectDir.Path, "input.txt"), "expected");

env.SetCurrentDirectory(decoyCwd.Path); // auto-restored when env is disposed

var task = new MyTask
{
    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
    Input = "input.txt", // relative
};
task.Execute().ShouldBeTrue();
```

**CRITICAL:** `Directory.SetCurrentDirectory` is **process-global**. xUnit parallelizes tests within a class by default — a CWD-mutating test will flake unless pinned to a non-parallel collection (`[Collection]` + `[CollectionDefinition(DisableParallelization = true)]`). The `IDisposable` restore protects sequential safety but not concurrent siblings.

### Pattern B: Cross-Instance Independence (for tasks doing relative-path resolution)

Two task instances, two `TaskEnvironment.ProjectDirectory` values, same relative input. Assert each task's output is rooted to its own project directory — proving resolution is per-instance, not shared.

### When NOT to Write a "Migration Test"

Attribute-only migrations (just `[MSBuildMultiThreadableTask]`, no `IMultiThreadableTask`) on tasks whose `Execute()` body contains no file/env/process/static-state interactions have nothing to test — a decoy-CWD test would pass whether the attribute is present or not. **Don't write the test.** Document the call-chain audit conclusion in the PR description instead.

### Test Hygiene

- Use `TestEnvironment` / `TransientTestFolder` for temp dirs; never `Path.GetTempPath() + Guid.NewGuid()` with manual `try/finally`.
- Hard-code expected assertion values; don't recompute them from `Path.Combine(...)` in the assertion — when the test fails you want to know what was actually wrong.

---

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
- [ ] `IMultiThreadableTask` on classes that use `TaskEnvironment` APIs, with default initializer `= TaskEnvironment.Fallback`
- [ ] Every `[Output]` property: exact string value matches pre-migration (Sin 1)
- [ ] Every `Log.LogError`/`LogWarning`: path in message matches pre-migration (use `OriginalValue`) (Sin 2)
- [ ] Every `Log.LogError(ex.Message …)` / `ex.FileName`: exception path sanitized to original input (Sin 7)
- [ ] Every `GetAbsolutePath` call: null/empty exception behavior matches old code path (Sin 3, 6)
- [ ] Every dictionary/set with path keys: canonicalization preserved (`GetCanonicalForm()`) (Sin 5)
- [ ] Every try-catch: absolutized value available in catch block where needed (Sin 4)
- [ ] Every `??` or `?.` added: verified it doesn't swallow a previously-thrown exception (Sin 3)
- [ ] No `Path.IsPathRooted` short-circuits around `GetAbsolutePath` — call unconditionally (Sin 8)
- [ ] No `AbsolutePath` leaks into user-visible strings unintentionally
- [ ] No behavior conditioned on "MT mode on/off" — `TaskEnvironment.Fallback` handles single-process case
- [ ] **Call chain traced end-to-end** for every helper invoked from `Execute()`:
    - No `Environment.CurrentDirectory` / `Directory.GetCurrentDirectory()` / `Path.GetFullPath(x)` without a base anywhere in the transitive call graph
    - No direct `Environment.Get/SetEnvironmentVariable` (route through `TaskEnvironment`)
    - No `static` mutable fields seeded from process state; replace with `ConcurrentDictionary` keyed on inputs
    - No `Console.*`, `Environment.Exit`, `Process.Kill`, `FailFast`
- [ ] ToolTask overrides audited (`GenerateFullPathToTool`, `SkipTaskExecution`, `ValidateParameters`); `ProcessStartInfo.FileName` is absolute, tool *arguments* stay relative
- [ ] No nested tasks created via `new …Task()` without explicit `TaskEnvironment` propagation
- [ ] No mutations of engine-owned env vars (e.g. `MSBUILD*` and the framework/SDK discovery vars) in MT mode
- [ ] Tests for custom tasks set `TaskEnvironment = TaskEnvironmentHelper.CreateForTest()` (built-in tasks have a default)
- [ ] Migration test follows Pattern A (decoy-CWD) **or** Pattern B (cross-instance independence), or PR explains why no test is meaningful
- [ ] CWD-mutating tests pinned to a non-parallel xUnit collection
- [ ] Cross-framework: tested on both net472 and net10.0
