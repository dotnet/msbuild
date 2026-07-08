# ITaskFactory infrastructure and AOT/trimming safety

**Status:** Implemented (honest RUC on the public task-factory contract); the AOT-safe replacement in §7 is a design proposal.

**Scope:** `Microsoft.Build` task creation/execution.

**Bottom line:** the `ITaskFactory` contract is inherently reflective (it loads task assemblies by name and
activates types at runtime), so it cannot be made trim/AOT-safe in place; it carries honest
`[RequiresUnreferencedCode]`, and an AOT host must fall back to JIT MSBuild for custom-task execution until a
closed-world task-registration mechanism (§7) exists.

## 1. Purpose

This document describes how MSBuild creates and executes tasks through the
`ITaskFactory` family, analyzes why that infrastructure is **fundamentally
incompatible with trimming and Native AOT** in its current form, and proposes
what an AOT-safe task mechanism would have to look like.

The honesty position below is MSBuild's
[fail-observably-never-silently design criterion](managing-trimming-and-aot.md#msbuilds-overriding-design-criterion-fail-observably-never-silently)
applied to a public contract: rather than suppress the warning - which would let a trimmed/AOT
host reach task creation and then fail confusingly - the surface carries honest
`[RequiresUnreferencedCode]`, so the incompatibility is visible at the boundary and a host can
fall back to a JIT MSBuild.

It also enforces a correctness principle: **the public `ITaskFactory` surface must not
be annotated in a way that tells consumers "this is trim-safe" when it is not.**
The public `ITaskFactory` / `ITaskFactory2` / `ITaskFactory3`
`Initialize` and `CreateTask` members carry `[RequiresUnreferencedCode]`, the
matching RUC is present on every implementer (so the build satisfies the IL2046
symmetry rule), and no boundary `[UnconditionalSuppressMessage]` masks the trim
warning on `RoslynCodeTaskFactory`'s public methods. The public contract tells the
truth: a caller that reaches task creation through the interface gets an honest
IL2026. §6 describes the annotation and §8 records the suppression inventory.

## 2. The ITaskFactory interface family

All in `src/Framework/`.

### `ITaskFactory` (public)

```csharp
public interface ITaskFactory
{
    string FactoryName { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    Type TaskType { get; }

    bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup,
                    string taskBody, IBuildEngine taskFactoryLoggingHost);

    TaskPropertyInfo[] GetTaskParameters();

    ITask CreateTask(IBuildEngine taskFactoryLoggingHost);

    void CleanupTask(ITask task);
}
```

### `ITaskFactory2 : ITaskFactory` (public)

Adds Runtime/Architecture-aware overloads:

```csharp
bool Initialize(string taskName, IDictionary<string, string> factoryIdentityParameters,
                IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody,
                IBuildEngine taskFactoryLoggingHost);
ITask CreateTask(IBuildEngine taskFactoryLoggingHost, IDictionary<string, string> taskIdentityParameters);
```

### `ITaskFactory3 : ITaskFactory2` (public)

Same shape, but keyed off the struct `TaskHostParameters` instead of an
`IDictionary<string,string>`:

```csharp
bool Initialize(string taskName, TaskHostParameters factoryIdentityParameters,
                IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody,
                IBuildEngine taskFactoryLoggingHost);
ITask CreateTask(IBuildEngine taskFactoryLoggingHost, TaskHostParameters taskIdentityParameters);
```

`ITaskFactory3`'s XML doc already discourages custom implementations and steers
authors toward the built-in factories — relevant below.

### Internal supporting interfaces

- `IOutOfProcTaskFactory` — `string? GetAssemblyPath()`. A marker that a factory
  can hand the engine an on-disk assembly path so the task can be run in a task
  host (out-of-process). Only MSBuild-shipped factories implement it; **custom
  factories cannot run out of process**.
- `ITaskFactoryBuildParameterProvider` — `IsMultiThreadedBuild`,
  `ForceOutOfProcessExecution`.

## 3. Built-in factories (what actually runs on .NET)

| Factory | Implements | net10.0 behavior | Reflection used |
|---|---|---|---|
| `AssemblyTaskFactory` (`src/Build/Instance/TaskFactories/`) | `ITaskFactory3` | **Active — the main path** | `TypeLoader.Load` (assembly load + type resolution), then `Activator.CreateInstance` via `TaskLoader.CreateTask`, or a `TaskHostTask` for out-of-proc |
| `IntrinsicTaskFactory` (`.../IntrinsicTasks/`) | `ITaskFactory` | Active | None — `new MSBuild()` / `new CallTarget()` directly |
| `RoslynCodeTaskFactory` (`src/Tasks/RoslynCodeTaskFactory/`) | `ITaskFactory`, `IOutOfProcTaskFactory` | **Active** | Compiles source at runtime, `Assembly.GetExportedTypes`, `Activator.CreateInstance` |
| `CodeTaskFactory` (`src/Tasks/`) | `ITaskFactory`, `IOutOfProcTaskFactory` | **Stub that throws** (`FEATURE_CODETASKFACTORY` is net472-only) | n/a on net core |
| `XamlTaskFactory` (`src/Tasks/XamlTaskFactory/`) | `ITaskFactory`, `IOutOfProcTaskFactory` | **Stub that throws** | n/a on net core |

So on .NET (where the analyzers run) the reflective task surface is really two
factories: **`AssemblyTaskFactory`** (every compiled task — i.e. essentially all
of them) and **`RoslynCodeTaskFactory`** (inline C#/VB).

## 4. Creation / execution flow

```
<UsingTask TaskName=... AssemblyFile=... />          (project XML, discovered at runtime)
        │
TaskRegistry.RegisterTasksFromUsingTaskElement       evaluates UsingTask, caches RegisteredTaskRecord
        │  GetRegisteredTask  ──►  creates the ITaskFactory, calls factory.Initialize(...)
        ▼
TaskFactoryWrapper (wraps ITaskFactory + LoadedType)
        │
TaskBuilder.ExecuteTask                              [RequiresUnreferencedCode]
        ▼
TaskExecutionHost.FindTask / InstantiateTask
        ├─ AssemblyTaskFactory ─► CreateTaskInstance ─► TaskLoader.CreateTask ─► Activator.CreateInstance(loadedType.Type)
        │                                            └► or TaskHostTask (out-of-proc)
        ├─ IntrinsicTaskFactory ─► new MSBuild()/new CallTarget()
        └─ custom / Roslyn ─► ITaskFactory[2|3].CreateTask(...) ─► Activator.CreateInstance(TaskType)
        ▼
TaskExecutionHost sets parameters via reflection (ReflectableTaskPropertyInfo / property get/set)
        ▼
ITask.Execute()
```

Two reflection mechanisms matter for trimming:

1. **Type discovery + instantiation.** `TypeLoader` resolves a type by name from
   an assembly that is named in the project file at runtime (`Assembly.Load`,
   `Assembly.LoadFrom`, `AssemblyLoadContext.LoadFromAssemblyPath`, or a
   `MetadataLoadContext` for cross-arch/out-of-proc). The instance is created
   with `Activator.CreateInstance`.
2. **Parameter binding.** `TaskExecutionHost` reads/writes task parameters by
   reflecting over the task type's public properties
   (`ReflectableTaskPropertyInfo`, `Type.GetProperty`/`GetProperties`).

## 5. Why this cannot be trim/AOT safe as-is

The trimmer/AOT compiler operate on a **closed world**: every type that can be
instantiated or reflected over must be statically discoverable from the app's
reference graph. MSBuild's task model violates that at four distinct points:

1. **Task assemblies are discovered at runtime.** `<UsingTask AssemblyFile=...>`
   / `AssemblyName=...` names an assembly that is not part of the host's compile
   graph. The trimmer cannot see it, so it cannot preserve it or anything in it.
2. **Tasks are instantiated reflectively.** `Activator.CreateInstance(taskType)`
   needs the parameterless constructor preserved. For a runtime-discovered type
   the trimmer has no way to know that.
3. **Parameters are bound reflectively.** Even if the task type survived, the
   trimmer can freely remove unused public property setters/getters; MSBuild then
   fails to set `[Required]`/`[Output]` parameters. This is the classic "trims to
   a non-working state" failure — and it is **silent**.
4. **Two factories compile code at runtime.** `RoslynCodeTaskFactory` /
   `CodeTaskFactory` invoke a compiler and load the result. Runtime code
   generation is categorically incompatible with AOT (`RequiresDynamicCode`) and
   undesirable under trimming.

Points 1–3 apply to **every** task, including the `AssemblyTaskFactory` path that
runs essentially all real-world tasks. The conclusion: **`ITaskFactory` is a
runtime plugin-loading contract. There is no annotation that makes the existing
contract trim-safe**, because the types it operates on do not exist in the
trimmed world.

### What `IsAotCompatible` actually promised

`src/Build/Microsoft.Build.csproj` sets, for net8.0+:

```xml
<IsAotCompatible Condition="...IsTargetFrameworkCompatible(..., 'net8.0')">true</IsAotCompatible>
```

The in-repo comment frames this as "enable the trim/AOT **analyzers**." But in
the .NET SDK, `IsAotCompatible=true` also implies **`IsTrimmable=true`** (along
with `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer`, `EnableAotAnalyzer`).
`IsTrimmable=true` is a **promise to the trimmer**: "this assembly opted in; trim
it, and assume the remaining warnings are handled." Every
`[UnconditionalSuppressMessage]` we add is us telling the trimmer that promise is
satisfied at that call site. Where it is not actually satisfied — the task-loading
paths — we have converted a loud build-time warning into a silent runtime break
for anyone who ever trims `Microsoft.Build`.

That is the heart of the concern: **if we mark the assembly trimmable, it must not
trim to a non-working state.** It still would if a host actually trims it — the
task model is structurally reflective — but the annotations no longer *hide* that.
The reflective engine paths and the public `ITaskFactory` contract now carry
honest `[RequiresUnreferencedCode]` (§6), so the incompatibility surfaces as an
IL2026 the caller can see rather than a silent runtime break. Genuinely AOT-safe
task execution still requires the closed-world mechanism in §7.

## 6. The honest annotations

The design rule is: *propagate `[RequiresUnreferencedCode]` (RUC) up the real call
chain; suppress only at boundaries the analyzer genuinely cannot cross.* The internal
engine paths follow it directly: `TaskBuilder.ExecuteTask`, `TaskExecutionHost.FindTask`,
`TaskRegistry.GetRegisteredTask`, `AssemblyTaskFactory.InitializeFactory`,
`TypeLoader.Load`, etc. are all RUC, so a caller that reaches them through the
internal API gets an honest IL2026.

The **public `ITaskFactory` contract** carries the requirement too. A public
interface implementation cannot carry RUC unless the **interface member** also
carries it (the IL2046 symmetry rule — the attribute must be present on both sides);
suppressing on the implementation instead (`RoslynCodeTaskFactory.CreateTask` /
`Initialize`) would leave the public method presenting to every caller and to the
trimmer as trim-safe, which is the trim-analysis equivalent of lying to the caller.
The contract is annotated honestly on both sides:

1. **`[RequiresUnreferencedCode]` on the public contract.** `ITaskFactory.Initialize`
   and `ITaskFactory.CreateTask`, plus the `ITaskFactory2` and `ITaskFactory3`
   `Initialize` / `CreateTask` overloads, now carry RUC
   (`src/Framework/ITaskFactory.cs`, `ITaskFactory2.cs`, `ITaskFactory3.cs`). The
   message states that task factories create tasks by reflecting over a task type
   discovered or generated at runtime, which is incompatible with trimming.
2. **Matching RUC on every implementer** (the IL2046 symmetry requirement):
   - `AssemblyTaskFactory` — all six `Initialize`/`CreateTask` members
     (ITaskFactory/2/3).
   - `IntrinsicTaskFactory` — `Initialize`/`CreateTask` (even though the bodies are
     `new MSBuild()` / `new CallTarget()`; the attribute must be present to match
     the interface).
   - `RoslynCodeTaskFactory` — the two public `ITaskFactory` members carry RUC that
     legally matches the interface; no suppression masks the warning on them. The
     genuinely unsafe work stays isolated in the private RUC helpers
     (`TryCompileAssembly`, `TryResolveCompiledTaskType`, `CreateTaskInstance`).
   - `CodeTaskFactory` and `XamlTaskFactory` — both the real (`net472`,
     `FEATURE_*`) implementations and the .NET-core throwing stubs.
3. **Internal dispatch matches.** `TaskExecutionHost.InitializeForBatch`
   / `InstantiateTask` / `CreateTaskHostTaskForOutOfProcFactory` and
   `TaskBuilder.InitializeAndExecuteTask` carry RUC so the internal chain that feeds
   the factory contract stays honest end to end.

With both sides annotated, a host that calls `ITaskFactory.CreateTask`/`Initialize`
through the interface gets an honest IL2026, and the build is clean on both `net10.0`
and `net472` under warnings-as-errors (0 IL warnings, 0 warnings, 0 errors). The
attributes compile on `net472`/`netstandard2.0` via the internal polyfill in
`src/Framework/Polyfills/AotTrimmingPolyfills.cs` (the trim analyzer only runs on
`net10.0`).

### Consequences accepted

- **Public-surface change.** Adding RUC to `ITaskFactory`/2/3 is a public-contract
  change (trim metadata, no managed-signature change). It passes the in-repo
  public-API baseline analyzers (RS0016 / ApiCompat = 0) but is the kind of change
  that should be called out for API review.
- **Third-party factories/tasks.** A third-party `ITaskFactory` implementation that
  enables trim analysis will now get IL2046 until it adds the matching RUC. That is
  the **correct** signal — their factory is not trim-safe either.
- **`RequiresDynamicCode`** still belongs on the two compiling factories
  (`RoslynCodeTaskFactory` / `CodeTaskFactory`) for the AOT (IL3050) story, tracked
  separately from the RUC annotations.

## 7. What an actually AOT-safe task mechanism would require

Because the incompatibility is structural, "fixing" `ITaskFactory` is not an
annotation exercise — it needs a **second, opt-in, closed-world mechanism**. The
shape:

1. **Static task registration via a source generator.** A task author (or the SDK)
   marks task types — e.g. `[MSBuildTask]` — in an assembly that is *referenced at
   compile time* by the host. A generator emits a static registry:
   - `taskName → Func<ITask>` (a typed `new MyTask()` delegate) so creation never
     calls `Activator.CreateInstance`.
   - Per-parameter typed accessors (`Action<ITask, object?>` setters,
     `Func<ITask, object?>` getters) so `TaskExecutionHost` binds parameters
     without `Type.GetProperty`/`GetProperties`. This replaces the reflective
     `ReflectableTaskPropertyInfo` on the AOT path.
   - The `TaskPropertyInfo[]` metadata (`[Required]`/`[Output]`, types) computed at
     compile time.
2. **Engine consults the static registry first.** `TaskRegistry` /
   `TaskExecutionHost` look up a statically-registered factory before falling back
   to `AssemblyTaskFactory`'s reflection. Under AOT, a task that is *not* statically
   registered produces a clean, deterministic error ("task X is not available in a
   trimmed/AOT host") instead of a silent failure.
3. **Closed-world constraint is explicit.** Statically-registered tasks only work
   when the task assembly is referenced by the host being trimmed — e.g. the SDK's
   own tasks compiled into a future AOT `dotnet build`. Arbitrary
   `<UsingTask AssemblyFile=...>` against an unreferenced DLL remains reflection-only
   and remains marked RUC. This is acceptable: AOT support is necessarily a subset.
4. **No runtime compilation on the AOT path.** `RoslynCodeTaskFactory` /
   `CodeTaskFactory` have no AOT story by construction; they stay RUC +
   `RequiresDynamicCode` and are simply unavailable in an AOT host.

This is a substantial feature (new public attribute/contract, a generator, engine
plumbing, and parameter-binding changes), not part of the current annotation pass.
It belongs in its own proposal; this document is the rationale for why it is the
only real path to AOT-safe task execution.

## 8. Inventory: the remaining suppressions, grouped by why each exists

Line numbers are approximate. The public-contract case §6 covers is **not** an
inventory row: those members carry honest `[RequiresUnreferencedCode]`, not a
suppression.

### A. Public-contract suppressions — must not exist (see §6)

A suppression on a public `ITaskFactory`/`ITask` member would tell consumers the path
is trim-safe when it is not. None exist: the `ITaskFactory`/2/3 `Initialize`/`CreateTask`
members and every implementer (`AssemblyTaskFactory`, `IntrinsicTaskFactory`,
`CodeTaskFactory`, `XamlTaskFactory`) carry matching RUC instead (§6). The analogous
`ITask.Execute` / XmlSerializer-based public tasks in **other assemblies** are tracked as Backlog -
see Category D - and can be revisited the same way.

### B. Boundary suppressions — RUC genuinely cannot flow further (message pumps, delegates, contracts)

These terminate an otherwise-honest RUC chain at a point the analyzer can't cross
(an `Action`/event delegate, an `INodePacket` switch, or a public contract that
can't carry RUC). They are defensible, but every one of them is a place a caller
loses the trim signal, so they belong in the same audit.

| Location | Code | Boundary |
|---|---|---|
| `BackEnd/Components/RequestBuilder/TaskHost.cs` ~336 | IL2026 | `IBuildEngine3.BuildProjectFilesInParallel` (public contract) |
| `BackEnd/Components/RequestBuilder/IntrinsicTasks/MSBuild.cs` ~514 | IL2026 | shared impl of intrinsic `MSBuild`/`CallTarget` `ITask.Execute` |
| `BackEnd/Node/InProcNode.cs` ~379 | IL2026 | node packet-pump `HandlePacket` |
| `BackEnd/Node/OutOfProcNode.cs` ~631 | IL2026 | node packet-pump `HandlePacket` |
| `BackEnd/Components/BuildRequestEngine/BuildRequestEngine.cs` ~1049 | IL2026 | `ActivateBuildRequest` driven from the `QueueAction` pump |
| `BackEnd/Components/BuildRequestEngine/BuildRequestEngine.cs` ~1068 | IL2026 | `Builder_On*Request` event handlers |
| `BackEnd/BuildManager/BuildManager.cs` ~1456 | IL2026 | `INodePacketHandler.PacketReceived` |
| `BackEnd/Components/SdkResolution/MainNodeSdkResolverService.cs` ~60 | IL2026 | `INodePacketHandler.PacketReceived` |
| `BuildCheck/Infrastructure/BuildCheckBuildEventHandler.cs` ~134 | IL2026 | build-event handler dispatch |
| `BuildCheck/Infrastructure/BuildCheckManagerProvider.cs` ~331 | IL2026 | `SetupChecksForNewProject` (custom-check materialization) |
| `Definition/ProjectCollection.cs` ~212/225/238/253 | IL2026 | ctors that pass no loggers (reflective logger path not exercised) |
| `Definition/ProjectCollection.cs` ~466 | IL2026 | `GlobalProjectCollection` singleton accessor |

### C. `UnrecognizedReflectionPattern` family — by-name resolution / filter delegates

These sit inside methods that are already RUC, or on `Func<Type,object,bool>`
filter delegates that cannot carry `[DynamicallyAccessedMembers]`. Not "lies" so
much as the residue of by-name type handling; listed for completeness.

| Location | Code(s) |
|---|---|
| `BackEnd/TaskExecutionHost/TaskExecutionHost.cs` ~1127, ~1809 | IL2057, IL2072 |
| `Instance/TaskRegistry.cs` ~1741, ~1743, ~1851 | IL2057, IL2096 |
| `Evaluation/Expander.cs` ~3828, 3987, 4250, 4300, 4433, 4435 | IL2072/2074/2096/2026 (property-function dispatch) |
| `Logging/LoggerDescription.cs` ~261, ~275 | IL2070 (`IsLoggerClass`/`IsForwardingLoggerClass` filters) |
| `src/Shared/TaskLoader.cs` ~37 | IL2070 (`IsTaskClass` filter) |

### D. Pre-existing suppressions in other assemblies - Backlog

Recorded so the contract decision in §6 and the XML-handling backlog can be applied consistently later.

| Location | Code(s) | Origin |
|---|---|---|
| `src/Tasks/SignFile.cs` ~46/48 | IL2026/IL3050 | XML handling backlog (`XmlSerializer`/crypto) |
| `src/Tasks/ManifestUtil/DeployManifest.cs` ~551/553 | IL2026/IL3050 | XML handling backlog (`XmlSerializer`) |
| `src/Tasks/GenerateManifestBase.cs` ~277/279 | IL2026/IL3050 | XML handling backlog (`XmlSerializer`) |
| `src/Tasks/ManifestUtil/TrustInfo.cs` ~533 | IL3050 | XML handling backlog (`XslCompiledTransform`) |
| `src/Tasks/WriteCodeFragment.cs` ~82 | IL2026 | attribute/code generation backlog (`CodeDom`) |
| `src/Tasks/XslTransformation.cs` ~104 | IL3050 | XML handling backlog (`XslCompiledTransform`) |
| `src/Tasks/BootstrapperUtil/BootstrapperBuilder.cs` ~133 | IL3050 | XML handling backlog (`XslCompiledTransform`) |
| `src/Tasks/RoslynCodeTaskFactory/RoslynCodeTaskFactory.cs` ~95 | IL3002 | single-file (not trimming); on-disk ref-assembly lookup |

The `RoslynCodeTaskFactory` public `ITask`-creating XmlSerializer-style tasks in
Category D are the same kind of public-contract suppression as Category A; if the
project adopts "RUC on the contract," `ITask.Execute` overrides like `SignFile`,
`GenerateManifestBase`, `DeployManifest` should be revisited the same way.

## 9. Recommendations

1. **Surface the public-contract change for API review.**
   `ITaskFactory`/`ITaskFactory2`/`ITaskFactory3` `Initialize`/`CreateTask` members
   carry `[RequiresUnreferencedCode]` and every implementer matches (§6), making the
   incompatibility visible instead of a "trimmable assembly + suppressed public task
   contract." Because this is a public-surface change (trim metadata), it should be
   called out for formal API review.
2. **Don't re-introduce Category A.** Don't add new
   `[UnconditionalSuppressMessage]` to public `ITaskFactory`/`ITask` members; if a
   warning appears there, route the reflection into a private RUC helper and/or add
   matching RUC on the contract rather than suppressing on the public method.
3. **Keep Category B/C honest internally.** Those are fine as long as the internal
   chain that feeds them is RUC (it is). Re-audit if any of them stop being fed by
   an RUC caller.
4. **Track the AOT-safe mechanism (§7) as its own proposal.** It is the only path
   to running tasks in a trimmed/AOT host, and it is additive (closed-world,
   opt-in), so it does not disturb the existing reflection-based model.

## 10. Related

- AOT follow-up items (the `IL2046` symmetry rule and the `ITaskFactory.TaskType`
  `[DynamicallyAccessedMembers]` API-review item) are covered by §7 above and the
  [strategy doc](aot-trimming-strategy.md).
- `documentation/wiki/Contributing-Tasks.md`, `documentation/wiki/Tasks.md` — task
  authoring model.
