# Trim / AOT annotation map and improvement strategies

**Status:** Living (annotation inventory).

A map of **where** the MSBuild engine carries trim/AOT annotations - `[RequiresUnreferencedCode]` (RUC),
`[RequiresDynamicCode]` (RDC), `[DynamicallyAccessedMembers]` (DAM), and
`[FeatureSwitchDefinition]`/`[FeatureGuard]` switches - followed by a prioritized set of **suggested
improvements** that drive the remaining `[UnconditionalSuppressMessage]` count down via feature guards and
the other strategies in the catalog.

This is the annotation-landscape companion to two documents (see the [folder README](README.md) for the full map):

- [aot-trim-suppressions.md](aot-trim-suppressions.md) - the live **suppression** tracker and the merged
  Backlog deep analysis.
- [aot-trimming-strategy.md](aot-trimming-strategy.md) - the **strategy catalog** (S1-S8) and decision
  framework these improvements draw on.

Scope: the three trim-enabled assemblies - **Microsoft.Build**, **Microsoft.Build.Framework**, and
**Microsoft.Build.Utilities**. `Microsoft.Build.Tasks` is not trim-enabled; its suppressions are tracked
as Backlog in the suppression tracker. Line numbers drift - search by member
name if a reference is stale.

## How the annotation kinds relate

| Attribute | What it says | How it is "discharged" |
| --- | --- | --- |
| `[RequiresUnreferencedCode]` (RUC) | "This member reflects over types/assemblies discovered at run time; trimming may remove what it needs." | Propagate up to a boundary, **or** put the reflective call behind a `[FeatureGuard]` switch, **or** restructure so there is no reflection (registration / direct construction). |
| `[RequiresDynamicCode]` (RDC) | "This member needs runtime code generation (not available under Native AOT)." | Same options; in MSBuild the only RDC member rooted is `Enum.GetValues(Type)`, proven unreachable via property functions. |
| `[DynamicallyAccessedMembers]` (DAM) | "Preserve *these* members of the `Type` that flows here." | The annotation **is** the fix - it makes a reflective member access trim-safe. The member types requested must match how the code actually reflects. |
| `[FeatureSwitchDefinition]` / `[FeatureGuard]` | "This AppContext switch is substituted to a constant under trim; treat the guarded branch as removed (and, for `[FeatureGuard]`, as discharging the RUC inside it)." | The trimmer folds the constant and machine-checks the guarded reflective branch is dropped, so no suppression is needed. |

The MSBuild design rule throughout: **fail observably, never silently** (see
[managing-trimming-and-aot.md](managing-trimming-and-aot.md)). Every gated-off reflective leaf raises a
reported build error (e.g. **MSB4283**, **MSB4282**) so an AOT host can detect the unsupported path and fall
back to a JIT MSBuild.

## Annotation inventory (counts)

Counts as of 2026-06-28. There are **no** `[RequiresDynamicCode]`
annotations; the two IL3050s are *suppressions* (rooted-but-unreachable `Enum.GetValues(Type)`), counted in
the suppression row.

| Category | Microsoft.Build | Microsoft.Build.Framework | Microsoft.Build.Utilities | Total |
| --- | --- | --- | --- | --- |
| `[RequiresUnreferencedCode]` | 116 | 17 | 0 | **133** |
| `[DynamicallyAccessedMembers]` | 11 | 10 | 2 | **23** |
| `[RequiresDynamicCode]` | 0 | 0 | 0 | **0** |
| `[FeatureSwitchDefinition]` | 0 | 8 | 0 | **8** |
| `[FeatureGuard]` (subset of the above) | 0 | 6 | 0 | **6** |
| Active `[UnconditionalSuppressMessage]` (in-scope) | 8 | 4 | 0 | **12** (+1 in the AOT harness) |

The high `[RequiresUnreferencedCode]` count in **Microsoft.Build** (116) is concentrated in the
logger / project-cache-plugin / solution-metaproject / SDK-resolution reflective subsystems and the public
entry points that reach them (`BuildManager`, `Project`, `ProjectInstance`, `BuildSubmission`,
`SolutionProjectGenerator`). Gating those subsystems behind feature switches - as
`EnableReflectiveTaskExecution` already did for the task chain - is what would let large stretches of that
RUC drop (see [Remaining follow-up work](#remaining-follow-up-work)).

## Where the `[RequiresUnreferencedCode]` annotations live

The 133 RUC annotations cluster into a handful of reflective subsystems. The leaf is reflective; the RUC
either propagates to an honest boundary or is held behind a feature guard.

| Subsystem | Representative members | Gated by a `[FeatureGuard]` today? |
| --- | --- | --- |
| **Reflective task execution** | [`TaskExecutionHost.FindTaskInRegistry` / `InstantiateTask`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs); [`TaskRegistry.GetRegisteredTask` / `LoadTaskFactory` / `CreateTaskFactory`](../../src/Build/Instance/TaskRegistry.cs) | **Yes** - `EnableReflectiveTaskExecution` gates the three `TaskExecutionHost` leaves; the chain above them is now non-RUC. |
| **Task parameter type by-name** | [`TaskRegistry.ResolveParameterTypeByName`](../../src/Build/Instance/TaskRegistry.cs) | **Yes** - `EnableReflectiveTaskParameterTypes` (registry-first, by-name fallback gated). |
| **Task assembly loaders** | [`TaskFactoryUtilities`](../../src/Framework/Utilities/TaskFactoryUtilities.cs) resolve/handler methods; [`CoreCLRAssemblyLoader`](../../src/Framework/Loader/CoreCLRAssemblyLoader.cs) (7 methods) | Partially - `EnableCustomPluginProbing` gates `MSBuildLoadContext`/`TaskEngineAssemblyResolver`. |
| **Public task-factory interfaces** | [`ITaskFactory` / `ITaskFactory2` / `ITaskFactory3`](../../src/Framework/ITaskFactory.cs) `Initialize` / `CreateTask` | No - preview cleanup candidate. The RUC annotations should be removed before ship if the remaining interface path can be made analyzer-clean; registered/intrinsic tasks already bypass it via non-interface methods. |
| **Reflective logger loading** | [`LoggerDescription.CreateForwardingLogger` / `CreateLogger`](../../src/Build/Logging/LoggerDescription.cs); [`OutOfProcNode.HandleNodeConfiguration`](../../src/Build/BackEnd/Node/OutOfProcNode.cs) | Partially - `EnableReflectiveLoggerLoading` gates the `LoggingService` forwarding-logger calls; the node-configuration leaf is **not yet** gated (the surviving `OutOfProcNode` IL2026). |
| **Project-cache plugins** | [`ProjectCacheService`](../../src/Build/BackEnd/Components/ProjectCache/ProjectCacheService.cs) (8 methods) | **No feature gate** - candidate (see [Remaining follow-up work](#remaining-follow-up-work)). |
| **Solution metaproject generation** | [`SolutionProjectGenerator`](../../src/Build/Construction/Solution/SolutionProjectGenerator.cs); [`ProjectInstance`](../../src/Build/Instance/ProjectInstance.cs) solution methods | No - reaches evaluation + SDK resolution. |
| **Build orchestration boundary** | [`BuildManager`](../../src/Build/BackEnd/BuildManager/BuildManager.cs) (logger/plugin/solution init); message pumps | Boundary; cannot itself carry the gate. |

## Where the `[DynamicallyAccessedMembers]` annotations live

The 23 DAM annotations are the *positive* trim fixes - they make a reflective access safe by preserving the
exact members the code touches.

| Family | Members | Requested member types | Correct? |
| --- | --- | --- | --- |
| **Property-function receivers** | [`Function._receiverType` + ctor param](../../src/Build/Evaluation/Expander.Function.cs); [`FunctionBuilder.SetReceiverType`](../../src/Build/Evaluation/Expander.FunctionBuilder.cs) | `PublicConstructors \| PublicMethods \| PublicProperties \| PublicFields` | **Yes** - property functions invoke constructors (`new`), call methods, read properties **and** fields (`MaxValue`); kept in sync with `Constants.PropertyFunctionMembers`. Narrowing would break field/constructor access. |
| **Loaded task types** | [`LoadedType` ctor + `TaskType`](../../src/Framework/Loader/LoadedType.cs); [`IntrinsicTaskFactory` ctor](../../src/Build/BackEnd/Components/RequestBuilder/IntrinsicTasks/IntrinsicTaskFactory.cs); [`TaskExecutionHost.CreateIntrinsicTaskFactoryWrapper`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) | `PublicParameterlessConstructor \| PublicProperties` | **Yes** - construct (`new`) + bind public properties; no fields/methods reflected. |
| **Registration generics** | [`TaskClassRegistry.Register<T>` / `CreateLoadedType`](../../src/Framework/TaskClassRegistry.cs); [`Utilities.Task.RegisterTask<T>`](../../src/Utilities/Task.cs); [`TaskParameterTypeRegistry.RegisterValueType<T>`](../../src/Framework/TaskParameterTypeRegistry.cs); [`TaskItem.RegisterTaskParameterValueType<T>`](../../src/Utilities/TaskItem.cs) | `PublicParameterlessConstructor \| PublicProperties` (task types) / `All` (parameter value types) | **Yes** - the generic type parameter carries the DAM so `typeof(T)` flows it to the `LoadedType` build; `All` for value-type parameters is deliberately conservative (any member can be marshalled). |
| **Plugin types** | [`ProjectCacheService.CreatePluginInstanceFromType` / `GetTypeFromAssemblyPath`](../../src/Build/BackEnd/Components/ProjectCache/ProjectCacheService.cs) | `PublicParameterlessConstructor` | **Yes** - plugin is only `Activator.CreateInstance`d. |
| **Reflection helpers** | [`TypeExtensions.InvokePublicMember` / `InvokeMemberPublicOnly`](../../src/Framework/Utilities/TypeExtensions.cs) | public member surface constant | **Yes** - paired with the IL2070 suppression; `BindingFlags.NonPublic` is rejected by the caller. |
| **Public contract** | [`ITaskFactory.TaskType`](../../src/Framework/ITaskFactory.cs) | `PublicProperties` | **Yes** - factories expose the task's bindable properties. |

## Feature switches

All eight live in [`FeatureSwitches.cs`](../../src/Framework/FeatureSwitches.cs) in Framework (the lowest
assembly), each mapped to an AppContext switch and a `RuntimeHostConfigurationOption` that supplies the
trimmed constant. Package consumers get the trimmed defaults through the Framework package's
`buildTransitive` targets; non-package consumers such as the in-repo AOT harness still re-declare them
locally because project-level `RuntimeHostConfigurationOption` items do not flow across project references.

| Switch | Default (JIT) | Trimmed | `[FeatureGuard]` | Gates |
| --- | --- | --- | --- | --- |
| `EnableCustomPluginProbing` | true | false | RUC | Plugin/task assembly probing (`MSBuildLoadContext`, `TaskEngineAssemblyResolver`). |
| `EnableAllPropertyFunctions` | false | false | RUC | Run-time property-function receiver type probing. |
| `RestrictPropertyFunctionReceivers` | false | **true** | - | Restricts instance receivers to the curated set; no RUC to guard. |
| `EnableSdkResolverDynamicLoading` | true | false | RUC | Dynamically-loaded SDK resolver plugins (else **MSB4282**). |
| `EnableConfigurationFileToolsets` | true | false | - | `.exe.config` toolset reader; drops `System.Configuration`. |
| `EnableReflectiveTaskExecution` | true | false | RUC | Reflective task load/instantiate/bind (else **MSB4283**). |
| `EnableReflectiveTaskParameterTypes` | true | false | RUC | By-name task parameter `Type.GetType` fallback. |
| `EnableReflectiveLoggerLoading` | true | false | RUC | `LoggerDescription.CreateForwardingLogger` reflective load (else **MSB4285**). |

## RUC / DAM correctness verification

The annotations were inventoried across the three assemblies, and the representative families plus every
site an automated pass flagged were re-read against their member bodies. **No incorrect annotation was
found.** Three candidate "narrowings" were considered and **rejected** because they would break working
behavior or contradict the proven analysis:

- **Do not narrow the property-function receiver DAM** to `PublicMethods | PublicProperties`. Property
  functions invoke constructors and read fields (e.g. `$([System.Int32]::MaxValue)`), and the allowed
  binding flags include `GetField`; the set must stay
  `PublicConstructors | PublicMethods | PublicProperties | PublicFields`, matching
  `Constants.PropertyFunctionMembers`.
- **Do not remove RUC from `LoggerDescription.CreateLogger()` / `CreateForwardingLogger()`.** Both delegate
  to the private reflective `CreateLogger(bool)`, so calling either reaches reflection - the RUC is honest;
  removing it would resurface IL2026 at the call.
- **Do not add `[RequiresDynamicCode]` to `Constants.InitializeAvailableMethods`.** Its IL3050 is a
  *rooted-but-unreachable* `Enum.GetValues(Type)` (kept only because `typeof(Enum)` roots the allowlist),
  not a live dynamic-code call; an author cannot supply a `Type` argument (MSB4185/MSB4186), proven under
  Native AOT by `PropertyFunctionAotTests`. The suppression with that justification is the correct marker.

## Remaining follow-up work

The canonical list of remaining work is [follow-up-work.md](follow-up-work.md). The items below are a
short summary of the annotation/suppression cleanup opportunities from that list. Each removes or shrinks an
*accurate* warning by **restructuring or gating**, not by silencing, and preserves observable failure. None is
required for the engine to be trim-correct today; they shrink the residual `Backlog` set.

### 1. Gate project-cache plugin loading behind a feature switch (S3)

[`ProjectCacheService`](../../src/Build/BackEnd/Components/ProjectCache/ProjectCacheService.cs)'s eight RUC
methods load plugin assemblies from disk and reflect over their types, but - unlike task and logger loading -
they sit behind **no** feature switch. Add an `EnableReflectiveProjectCachePlugins` (or generalize the
existing `EnableCustomPluginProbing`) `[FeatureSwitchDefinition]` + `[FeatureGuard]`, default on under the
JIT, whose disabled branch reports an observable error (mirroring the BuildCheck acquisition guard, which is
the reference shape). Effect: the trimmer drops the plugin path from a trimmed image and an AOT host that
requests a project-cache plugin fails observably instead of crashing in reflection.

### 2. Gate the node forwarding-logger leaf (removes the `OutOfProcNode` IL2026)

The surviving [`OutOfProcNode.HandlePacket`](../../src/Build/BackEnd/Node/OutOfProcNode.cs) IL2026 exists
only because `HandleNodeConfiguration` initializes node forwarding loggers by reflection. The
`EnableReflectiveLoggerLoading` switch already exists and already gates the equivalent
`LoggerDescription.CreateForwardingLogger` calls in `LoggingService`. Extend that guard to the
node-configuration leaf so `HandleNodeConfiguration` can drop its RUC; the message-pump suppression on
`HandlePacket` then has nothing left to silence and can be removed.

### 3. Gate solution-metaproject generation (chips at the `BuildManager` IL2026)

The [`BuildManager.PacketReceived`](../../src/Build/BackEnd/BuildManager/BuildManager.cs) IL2026 reaches
evaluation, SDK resolution (already behind `EnableSdkResolverDynamicLoading`), logger loading (behind
`EnableReflectiveLoggerLoading`), **and** solution-metaproject generation
([`SolutionProjectGenerator`](../../src/Build/Construction/Solution/SolutionProjectGenerator.cs)), which is
still ungated. A `EnableSolutionMetaprojectGeneration`-style guard (observable failure when off) would
remove the last ungated reflective subsystem the work-queue boundary reaches. The boundary itself cannot
carry the gate; this is the leaf-gate pattern (S3) applied one subsystem at a time, exactly as
`EnableReflectiveTaskExecution` cleared the task chain.

### 4. Reuse the parameter-type registry for the serialization path (S5)

The one remaining Group B row -
[`TaskRegistry.TranslatorForTaskParameterValue`](../../src/Build/Instance/TaskRegistry.cs) (IL2057) -
reconstructs a task parameter type from a serialized assembly-qualified name. Its sibling `<ParameterGroup>`
site was already retired by [`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs).
The serialization path is a candidate to consult the same registry first (reflection-free for known types)
and gate the by-name `Type.GetType` fallback behind `EnableReflectiveTaskParameterTypes`. The open-world
value-type half (`IsValueType` admits any `struct`) bounds how far this can go, so it shrinks rather than
fully removes the row; see [task-parameter-types.md](task-parameter-types.md).

### 5. Remove task-factory interface RUC before ship

The RUC annotations on the public [`ITaskFactory` / `ITaskFactory2` / `ITaskFactory3`](../../src/Framework/ITaskFactory.cs)
interface members are part of the preview AOT annotation work, not a permanent shipped contract. Before GA,
try to remove those attributes rather than treating them as non-actionable. Registered and intrinsic tasks
already avoid the RUC interface methods by constructing through non-interface methods
(`RegisteredTaskFactory.CreateRegisteredTask`, `IntrinsicTaskFactory.CreateIntrinsicTask`), so the cleanup is
to prove any remaining interface call path is analyzer-clean, feature-gated, or fails observably when the
reflective path is disabled.

### Not actionable

- The **`TypeExtensions` reflection helpers** and the **two `Enum.GetValues(Type)` AOT rows** are provable
  false positives (Vetted) and stay suppressed.
