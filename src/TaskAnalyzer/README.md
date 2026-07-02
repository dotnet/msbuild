# MSBuild Thread-Safe Task Analyzer

A Roslyn analyzer that detects unsafe API usage in MSBuild task implementations. It guides task authors toward thread-safe patterns required for MSBuild's multithreaded task execution mode, where multiple tasks may run concurrently in the same process.

The package also includes a Roslyn diagnostic suppressor for nullable warning `CS8618` on task properties marked with `Microsoft.Build.Framework.RequiredAttribute`, since MSBuild guarantees those inputs are initialized before task execution.

## Background

MSBuild is introducing multithreaded task execution via `IMultiThreadableTask`. Tasks opting into this mode share a process and can no longer safely use process-global state like environment variables, the current directory, or `Console` output. The `TaskEnvironment` abstraction provides per-task isolated access to these resources.

This analyzer catches unsafe API usage at compile time and offers code fixes to migrate to the safe `TaskEnvironment` alternatives.

## Diagnostic Rules

| ID | Severity | Scope | Title |
|---|---|---|---|
| **MSBuildTask0001** | Error | All `ITask` implementations | API is never safe in MSBuild tasks |
| **MSBuildTask0002** | Warning | All `ITask` implementations | API requires `TaskEnvironment` alternative |
| **MSBuildTask0003** | Warning | All `ITask` implementations | File system API requires absolute path |
| **MSBuildTask0004** | Warning | All `ITask` implementations | API may cause issues in multithreaded tasks |
| **MSBuildTask0005** | Warning | All `ITask` implementations | Transitive unsafe API usage in task call chain |
| **MSBuildTask0006** | Info | Multithreaded tasks (`IMultiThreadableTask` or `[MSBuildMultiThreadableTask]`) | Prefer typed path parameter over string |
| **MSBuildTask0007** | Info | Multithreaded tasks (`IMultiThreadableTask` or `[MSBuildMultiThreadableTask]`) | Prefer `ITaskItem<T>` over manual ItemSpec parsing |

### MSBuildTask0001 — Critical: No Safe Alternative

These APIs affect the entire process or interfere with build infrastructure. They are **errors** and should never appear in any MSBuild task.

| API | Why it's banned |
|---|---|
| `Console.*` (all members) | Interferes with build logging; use `Log.LogMessage` |
| `Console.ReadLine`, `Console.Read` | May cause deadlocks in automated builds |
| `Environment.Exit`, `Environment.FailFast` | Terminates the entire MSBuild process |
| `Process.Kill` | May terminate the MSBuild host process |
| `ThreadPool.SetMinThreads`, `SetMaxThreads` | Modifies process-wide thread pool settings |
| `CultureInfo.DefaultThreadCurrentCulture` | Affects culture of all new threads in process |
| `CultureInfo.DefaultThreadCurrentUICulture` | Affects UI culture of all new threads |
| `Directory.SetCurrentDirectory` | Modifies process-wide working directory |

> **Note:** `Console.*` is detected at the **type level** — every member of `System.Console` is flagged, including properties like `Console.Out`, `Console.Error`, `Console.BufferWidth`, and any members added in future .NET versions. This also catches `using static System.Console` patterns.

### MSBuildTask0002 — Use TaskEnvironment Alternative

These APIs access process-global state that varies per task in multithreaded mode.

| Banned API | Replacement |
|---|---|
| `Environment.CurrentDirectory` | `TaskEnvironment.ProjectDirectory` |
| `Directory.GetCurrentDirectory()` | `TaskEnvironment.ProjectDirectory` |
| `Environment.GetEnvironmentVariable()` | `TaskEnvironment.GetEnvironmentVariable()` |
| `Environment.SetEnvironmentVariable()` | `TaskEnvironment.SetEnvironmentVariable()` |
| `Environment.GetEnvironmentVariables()` | `TaskEnvironment.GetEnvironmentVariables()` |
| `Environment.ExpandEnvironmentVariables()` | Use `TaskEnvironment.GetEnvironmentVariable()` per variable |
| `Environment.GetFolderPath()` | Use `TaskEnvironment.GetEnvironmentVariable()` |
| `Path.GetFullPath()` | `TaskEnvironment.GetAbsolutePath()` |
| `Path.GetTempPath()` | `TaskEnvironment.GetEnvironmentVariable("TMP")` |
| `Path.GetTempFileName()` | `TaskEnvironment.GetEnvironmentVariable("TMP")` |
| `Process.Start()` (all overloads) | `TaskEnvironment.GetProcessStartInfo()` |
| `new ProcessStartInfo()` (all overloads) | `TaskEnvironment.GetProcessStartInfo()` |

### MSBuildTask0003 — File Paths Must Be Absolute

File system APIs that accept a path parameter will resolve relative paths against the process working directory — which is shared and unpredictable in multithreaded mode.

**Monitored types:** `File`, `Directory`, `FileInfo`, `DirectoryInfo`, `FileStream`, `StreamReader`, `StreamWriter`, `FileSystemWatcher`

The analyzer inspects parameter names to determine which arguments are paths (e.g., `path`, `fileName`, `sourceFileName`, `destFileName`) and skips non-path string parameters (e.g., `contents`, `searchPattern`). Named arguments are handled correctly.

**Recognized safe patterns** (suppress the diagnostic):

```csharp
// 1. TaskEnvironment.GetAbsolutePath()
File.Exists(TaskEnvironment.GetAbsolutePath(relativePath))

// 2. Implicit conversion from AbsolutePath
AbsolutePath abs = TaskEnvironment.GetAbsolutePath(relativePath);
File.Exists(abs)  // implicit conversion to string

// 3. FileInfo.FullName / DirectoryInfo.FullName
new FileInfo(somePath).FullName  // already absolute

// 4. ITaskItem.GetMetadata("FullPath")
File.Exists(item.GetMetadata("FullPath"))
File.Exists(item.GetMetadataValue("FullPath"))

// 5. Argument already typed as AbsolutePath
void Helper(AbsolutePath p) => File.Exists(p);
```

### MSBuildTask0004 — Potential Issue (Review Required)

These APIs may cause version conflicts or other issues in a shared task host.

| API | Concern |
|---|---|
| `Assembly.Load`, `LoadFrom`, `LoadFile` | May cause version conflicts in shared task host |
| `Assembly.LoadWithPartialName` | Obsolete and may cause version conflicts |
| `Activator.CreateInstance(string, string)` | May cause version conflicts |
| `Activator.CreateInstanceFrom` | May cause version conflicts |
| `AppDomain.Load`, `CreateInstance`, `CreateInstanceFrom` | May cause version conflicts |

### MSBuildTask0006 — Prefer Typed Path Parameters

When a task has a `string` input property and converts it to `AbsolutePath`, `FileInfo`, or `DirectoryInfo` inside the task body, the analyzer suggests changing the property type directly. MSBuild can bind these types automatically.

**Detected patterns:**

```csharp
// ⚠️ MSBuildTask0006: Consider changing 'InputPath' from 'string' to 'AbsolutePath'
public class MyTask : Task
{
    public string InputPath { get; set; }

    public override bool Execute()
    {
        var abs = new AbsolutePath(InputPath);           // flagged
        var abs2 = TaskEnvironment.GetAbsolutePath(InputPath); // flagged
        var fi = new FileInfo(InputPath);                 // flagged (suggests FileInfo)
        var di = new DirectoryInfo(InputPath);            // flagged (suggests DirectoryInfo)
        return true;
    }
}
```

**Not flagged:** `[Output]` properties, non-public properties, read-only properties, values from method calls or literals.

### MSBuildTask0007 — Prefer `ITaskItem<T>` Over ItemSpec Parsing

When a task has an `ITaskItem` or `ITaskItem[]` input property and parses `ItemSpec` to a value type or path type, the analyzer suggests using `ITaskItem<T>` instead.

**Detected patterns:**

```csharp
// ⚠️ MSBuildTask0007: Consider changing 'Item' from 'ITaskItem' to 'ITaskItem<int>'
public class MyTask : Task
{
    public ITaskItem Item { get; set; }
    public ITaskItem[] Items { get; set; }

    public override bool Execute()
    {
        int value = int.Parse(Item.ItemSpec);             // flagged
        bool flag = Convert.ToBoolean(Item.ItemSpec);     // flagged
        var abs = new AbsolutePath(Item.ItemSpec);        // flagged (suggests ITaskItem<AbsolutePath>)

        foreach (var item in Items)
        {
            int v = int.Parse(item.ItemSpec);             // flagged (suggests ITaskItem<int>[])
        }

        // FileInfo/DirectoryInfo through AbsolutePath intermediary:
        AbsolutePath absPath = TaskEnvironment.GetAbsolutePath(Item.ItemSpec);
        var fi = new FileInfo(absPath);                   // flagged (suggests ITaskItem<FileInfo>)
        var di = new DirectoryInfo(absPath);              // flagged (suggests ITaskItem<DirectoryInfo>)

        // System.IO consumption sites infer the most specific type:
        File.Delete(TaskEnvironment.GetAbsolutePath(Item.ItemSpec));   // flagged (suggests ITaskItem<FileInfo>)
        Directory.CreateDirectory(absPath);                            // flagged (suggests ITaskItem<DirectoryInfo>)
        using var s = new FileStream(Item.ItemSpec, FileMode.Open);    // flagged (suggests ITaskItem<FileInfo>)

        var combined = Path.Combine(Item.ItemSpec, "sub");             // flagged (suggests ITaskItem<AbsolutePath>)

        return true;
    }
}
```

**Biasing toward `FileInfo`/`DirectoryInfo`:** When a rooted path flows into a `System.IO.File.*` call or a `FileStream`/`StreamReader`/`StreamWriter` constructor, the analyzer suggests `ITaskItem<FileInfo>`; when it flows into a `System.IO.Directory.*` call, it suggests `ITaskItem<DirectoryInfo>`. These more specific suggestions replace the generic `ITaskItem<AbsolutePath>` for the same property. Tracing follows both a direct `item.ItemSpec` and an `AbsolutePath` intermediary, including a local that is declared and then assigned once (the common `AbsolutePath? p = null; try { p = GetAbsolutePath(...); }` pattern). If one property is used as both a file and a directory, the analyzer falls back to `ITaskItem<AbsolutePath>`.

**`Path.Combine`:** A task input flowing into *any* argument position of `Path.Combine` is flagged (each distinct property is reported once per call), since the value is being used to build an absolute path.

**Array properties:** When the source property is `ITaskItem[]`, the suggestion correctly formats as `ITaskItem<T>[]` (brackets outside the angle brackets), e.g., `ITaskItem<AbsolutePath>[]`.

**Not flagged:** Metadata access (`item.GetMetadata(...)`), `[Output]` properties, non-task classes.

## Analysis Scope

The analyzer determines what to check based on the type declaration:

| Type | Rules Applied |
|---|---|
| Any class implementing `ITask` | MSBuildTask0001–MSBuildTask0005 |
| Multithreaded tasks (`IMultiThreadableTask` or `[MSBuildMultiThreadableTask]`) | MSBuildTask0006–MSBuildTask0007 |
| Class implementing `IMultiThreadableTask` | All seven rules |
| Class with `[MSBuildMultiThreadableTask]` attribute | All seven rules |
| Helper class with `[MSBuildMultiThreadableTaskAnalyzed]` attribute | MSBuildTask0001–MSBuildTask0005 |
| Regular class (no task interface or attribute) | Not analyzed |

The `[MSBuildMultiThreadableTaskAnalyzed]` attribute allows opting helper classes into **direct** analysis by the `MultiThreadableTaskAnalyzer` (MSBuildTask0001–0004). Without it, only classes implementing `ITask` receive per-line diagnostics and code fixes for those rules. The **transitive** analyzer (MSBuildTask0005) already discovers helpers via call graph analysis, but it reports only at the task entry point. Adding this attribute to a helper class gives you inline diagnostics and code fixes directly in the helper's source.

**When to use:** Apply `[MSBuildMultiThreadableTaskAnalyzed]` to utility or helper classes that are primarily used by multithreadable tasks and where you want immediate in-editor feedback (squiggles and code fixes) on unsafe APIs within those helpers.

### Severity Levels

- **MSBuildTask0001** is always **Error** — these APIs are never safe in any MSBuild task.
- **MSBuildTask0002–MSBuildTask0005** report as **Warning** for all task types.
- **MSBuildTask0006–MSBuildTask0007** report as **Info** — these are modernization suggestions, not correctness issues.

## Code Fixes

The analyzer ships with a code fix provider that offers automatic replacements:

| Diagnostic | Fix |
|---|---|
| MSBuildTask0002: `Environment.GetEnvironmentVariable(x)` | → `TaskEnvironment.GetEnvironmentVariable(x)` |
| MSBuildTask0002: `Environment.SetEnvironmentVariable(x, y)` | → `TaskEnvironment.SetEnvironmentVariable(x, y)` |
| MSBuildTask0002: `Environment.GetEnvironmentVariables()` | → `TaskEnvironment.GetEnvironmentVariables()` |
| MSBuildTask0002: `Path.GetFullPath(x)` | → `TaskEnvironment.GetAbsolutePath(x)` |
| MSBuildTask0002: `Environment.CurrentDirectory` | → `TaskEnvironment.ProjectDirectory` |
| MSBuildTask0002: `Directory.GetCurrentDirectory()` | → `TaskEnvironment.ProjectDirectory` |
| MSBuildTask0003: `File.Exists(relativePath)` | → `File.Exists(TaskEnvironment.GetAbsolutePath(relativePath))` |
| MSBuildTask0006: `new AbsolutePath(InputPath)` | → Retype `InputPath` to `AbsolutePath` and replace conversion with direct property usage |
| MSBuildTask0006: `new FileInfo(FilePath)` / `new DirectoryInfo(DirPath)` | → Retype property to `FileInfo`/`DirectoryInfo` and replace conversion with direct property usage |
| MSBuildTask0007: `int.Parse(Item.ItemSpec)` | → Retype `Item` to ``ITaskItem<int>`` and replace parse with `Item.Value` |
| MSBuildTask0007: `new FileInfo(item.ItemSpec)` in `foreach` over `ITaskItem[]` | → Retype source property to ``ITaskItem<FileInfo>[]`` and replace with `item.Value` |

The MSBuildTask0003 fixer intelligently finds the first **unwrapped** path argument rather than blindly wrapping the first argument — so for `File.Copy(safePath, unsafePath)` it correctly wraps the second argument.

The MSBuildTask0006/MSBuildTask0007 fixer is conservative by design: it only offers a fix when every reference to the property can be safely rewritten as part of the same change, so the resulting code keeps compiling after the property type is updated.

## Compiler Diagnostic Suppressions

The analyzer package suppresses `CS8618` for non-nullable properties when all of the following are true:

- The property is marked with `Microsoft.Build.Framework.RequiredAttribute`
- The containing type implements `Microsoft.Build.Framework.ITask` (including via `Microsoft.Build.Utilities.Task`)
- The warning is the nullable initialization warning emitted by the C# compiler

This lets task authors omit manual `= null!;` initializers for required MSBuild task inputs.

## Installation

### Project Reference (development)

Reference the analyzer project directly:

```xml
<ItemGroup>
  <ProjectReference Include="..\TaskAnalyzer\TaskAnalyzer.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### NuGet Package (future)

When packaged as a NuGet analyzer, add it as a package reference:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Build.TaskAuthoring.Analyzer" Version="1.0.0"
                    PrivateAssets="all" />
</ItemGroup>
```

### MSBuild Tasks Build

To enable the analyzer in the MSBuild Tasks project build, pass `/p:BuildAnalyzer=true`:

```
dotnet build src/Tasks/Microsoft.Build.Tasks.csproj /p:BuildAnalyzer=true
```

## Example

**Before** (unsafe):

```csharp
public class CopyFiles : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    public string Source { get; set; }
    public string Destination { get; set; }

    public override bool Execute()
    {
        Console.WriteLine("Copying files...");          // ❌ MSBuildTask0001
        var tmp = Path.GetTempPath();                    // ❌ MSBuildTask0002
        var envVar = Environment.GetEnvironmentVariable("MY_VAR"); // ❌ MSBuildTask0002
        File.Copy(Source, Destination);                  // ❌ MSBuildTask0003 (×2)
        return true;
    }
}
```

**After** (safe):

```csharp
public class CopyFiles : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    public string Source { get; set; }
    public string Destination { get; set; }

    public override bool Execute()
    {
        Log.LogMessage("Copying files...");              // ✅ Use build logging
        var tmp = TaskEnvironment.GetEnvironmentVariable("TMP"); // ✅ Per-task env
        var envVar = TaskEnvironment.GetEnvironmentVariable("MY_VAR"); // ✅ Per-task env
        File.Copy(                                       // ✅ Absolute paths
            TaskEnvironment.GetAbsolutePath(Source),
            TaskEnvironment.GetAbsolutePath(Destination));
        return true;
    }
}
```

## Tests

135 tests covering all rules, safe patterns, edge cases, code fixes, and compiler diagnostic suppression:

```
cd src/TaskAnalyzer.Tests
dotnet test
```

## Architecture

| File | Purpose |
|---|---|
| `MultiThreadableTaskAnalyzer.cs` | Core analyzer — `RegisterSymbolStartAction` scopes per type, `RegisterOperationAction` checks each API call |
| `MultiThreadableTaskCodeFixProvider.cs` | Code fixes for MSBuildTask0002 and MSBuildTask0003 |
| `BannedApiDefinitions.cs` | ~50 banned API entries resolved via `DocumentationCommentId` for O(1) symbol lookup |
| `SharedAnalyzerHelpers.cs` | Shared path safety analysis, banned API resolution, and interface checking helpers |
| `DiagnosticDescriptors.cs` | Seven diagnostic descriptors in category `MSBuild.TaskAuthoring` |
| `DiagnosticIds.cs` | Public constants: `MSBuildTask0001`–`MSBuildTask0007` |
| `PreferTypedParameterAnalyzer.cs` | Analyzer for MSBuildTask0006 and MSBuildTask0007 — detects manual path construction, ItemSpec parsing, Path.Combine usage (any argument position), helper method wrapping, FileInfo/DirectoryInfo construction through AbsolutePath intermediaries, and System.IO consumption sites (`File.*`/`Directory.*`/`FileStream`/`StreamReader`/`StreamWriter`) that bias suggestions toward `FileInfo`/`DirectoryInfo` |

### Performance

- **O(1) banned API lookup** via `Dictionary<ISymbol, BannedApiEntry>` with `SymbolEqualityComparer`
- **Per-type scoping in MultiThreadableTaskAnalyzer** via `RegisterSymbolStartAction` — operations outside task classes are never analyzed
- **Compilation-wide scan in TransitiveCallChainAnalyzer** — traces call chains across all methods to detect transitive banned API usage from task entry points
- **No LINQ on hot paths** — `ImplementsInterface` uses explicit loop
- **Static `AnalyzeOperation`** — no instance state captured
- **Cached banned API definitions** — built once per compilation via `static readonly` field
- **Type-level Console ban** — avoids enumerating 30+ `Console` method overloads

## Related

- [Multithreaded Task Execution Spec](https://github.com/dotnet/msbuild/pull/12583)
- [Analyzer Implementation PR](https://github.com/dotnet/msbuild/pull/12143)
- [IMultiThreadableTask Interface](../Framework/IMultiThreadableTask.cs)
- [TaskEnvironment Class](../Framework/TaskEnvironment.cs)
- [Migration Skill Guide](../../.github/skills/multithreaded-task-migration/SKILL.md)
