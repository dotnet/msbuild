# MSBuild trim / Native AOT follow-up work

**Status:** Living follow-up list.

This is the canonical list of known work that remains after the initial trim/AOT annotation and host-registration work. It intentionally does not repeat the full analysis from the strategy, suppression, or per-area documents; each item links to the owning implementation surface and the deeper note that explains the design.

All work here still follows the strategy guide's rule: **fail observably, never silently**. If a path cannot run under trimming or Native AOT, it must either be removed from the trimmed closure, replaced with a closed-world registration path, or fail with a reported error that a host can use to fall back to a JIT MSBuild.

## Product follow-ups

### 1. Gate project-cache plugin loading

- **Strategy:** S2/S3 feature gate, likely a new `EnableReflectiveProjectCachePlugins` switch or a carefully-scoped reuse of `EnableCustomPluginProbing`.
- **Implementation surface:** [`ProjectCacheService`](../../src/Build/BackEnd/Components/ProjectCache/ProjectCacheService.cs) and the `BuildManager` / `Project` / `ProjectInstance` build entry points that reach it.
- **Why it remains:** project-cache plugins load assemblies from disk and reflect over their types, but unlike task execution, SDK resolver loading, BuildCheck acquisition, and logger loading, this subsystem is not yet behind a trim-time feature switch.
- **Expected shape:** keep JIT behavior unchanged; when disabled in a trimmed/AOT host and a cache plugin is configured, fail observably with a reported build error rather than attempting plugin reflection.
- **Deeper context:** [aot-annotation-map.md](aot-annotation-map.md), remaining follow-up work.

### 2. Finish the node forwarding-logger leaf

- **Strategy:** S2 feature gate using the existing `EnableReflectiveLoggerLoading` switch.
- **Implementation surface:** [`OutOfProcNode.HandleNodeConfiguration`](../../src/Build/BackEnd/Node/OutOfProcNode.cs) and its call from `HandlePacket`.
- **Why it remains:** `LoggingService` already gates equivalent forwarding-logger creation, but the out-of-proc node configuration path still carries the surviving `OutOfProcNode.HandlePacket` IL2026 boundary suppression.
- **Expected shape:** gate the node forwarding-logger initialization leaf, then remove the message-pump suppression if no other RUC path remains through that packet arm.
- **Deeper context:** [aot-trim-suppressions.md](aot-trim-suppressions.md), `OutOfProcNode.HandlePacket` row.

### 3. Gate solution-metaproject generation

- **Strategy:** S2 feature gate with observable failure when a trimmed/AOT host is asked to build a solution path that needs generated metaprojects.
- **Implementation surface:** [`SolutionProjectGenerator`](../../src/Build/Construction/Solution/SolutionProjectGenerator.cs), solution-loading helpers in [`ProjectInstance`](../../src/Build/Instance/ProjectInstance.cs), and the `BuildManager.PacketReceived` boundary that can reach them.
- **Why it remains:** solution metaproject generation still pulls evaluation / SDK / logger surfaces through a message-pump boundary. Some of those leaves are now gated, but the solution generation subsystem itself is not.
- **Expected shape:** keep ordinary JIT solution builds unchanged; in a trimmed/AOT host, fail observably for solution-build execution paths the host cannot support, allowing fallback.
- **Deeper context:** [aot-annotation-map.md](aot-annotation-map.md), remaining follow-up work.

### 4. Reuse the task parameter type registry for serialized task parameter types

- **Strategy:** S5 registry-first lookup, with a gated by-name fallback for anything still open-world.
- **Implementation surface:** `TaskRegistry.TranslatorForTaskParameterValue` in [`TaskRegistry.cs`](../../src/Build/Instance/TaskRegistry.cs).
- **Why it remains:** the `<UsingTask><ParameterGroup>` parser now uses `TaskParameterTypeRegistry`, but task-host serialization still reconstructs a parameter type from a serialized assembly-qualified name with `Type.GetType(string)`, producing the remaining IL2057 row.
- **Expected shape:** consult `TaskParameterTypeRegistry` first for known serialized names; keep or further isolate the by-name fallback for genuinely open-world cases. This may shrink rather than fully remove the row because arbitrary value types remain legal.
- **Deeper context:** [task-parameter-types.md](task-parameter-types.md) and [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md).

### 5. Source-generated task parameter binder

- **Strategy:** S5 closed-world registration, extending task class registration from construction into parameter binding.
- **Implementation surface:** the host task registry (`TaskClassRegistry` / `Task.RegisterTask`) and [`TaskExecutionHost`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) parameter get/set paths.
- **Why it remains:** registered task classes run under AOT today by rooting public constructors and properties, but parameter binding still uses reflection over rooted properties. That is trim-safe for registered types, but a generated binder would remove this reflection and reduce rooting pressure.
- **Expected shape:** a generator or explicit registration surface emits task metadata plus strongly typed setters/getters for registered tasks.
- **Deeper context:** [task-class-registration-api.md](../specs/task-class-registration-api.md) and [task-factory-aot.md](task-factory-aot.md).

### 6. Verify package feature-switch defaults in the SDK AOT publish path

- **Strategy:** P-E validation / packaging follow-up.
- **Implementation surface:** [`Microsoft.Build.Framework.targets`](../../src/Framework/buildTransitive/Microsoft.Build.Framework.targets) in the Framework package and the consuming .NET SDK AOT publish.
- **Why it remains:** the Framework package now carries buildTransitive `RuntimeHostConfigurationOption` defaults for package consumers, and the in-repo AOT harness re-declares them for project-reference validation. The remaining work is to verify the SDK's real AOT publish consumes the package defaults as intended.
- **Expected shape:** inspect the SDK publish response file or equivalent output and confirm the `Microsoft.Build.*` feature settings are supplied without manual duplication.
- **Deeper context:** [managing-trimming-and-aot.md §6.5](managing-trimming-and-aot.md#65-how-a-librarys-switch-reaches-a-consumer-transitivity-defaulting-override) and [sdk-msbuild-object-model-audit.md](sdk-msbuild-object-model-audit.md).

### 7. Design an AOT-safe binding for typed `TaskItem<T>` / `ITaskItem<T>` parameters

- **Strategy:** S3 feature check today (a `RuntimeFeature.IsDynamicCodeSupported` guard that fails observably); a durable strategy likely needs closed-world registration or a source-generated wrapper factory.
- **Implementation surface:** `CreateTaskItemOfT` in [`TaskExecutionHost`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs).
- **Why it remains:** the typed task-parameter feature (`TaskItem<T>` / `ITaskItem<T>`) was integrated from upstream after the initial AOT annotation work. Wrapping an `ITaskItem` into a closed-generic `TaskItem<T>` uses `Type.MakeGenericType` plus an expression-tree `Compile()`, both of which require runtime code generation (IL3050). To keep the AOT analyzers clean after the rebase, the wrapper is guarded with `RuntimeFeature.IsDynamicCodeSupported` and throws `NotSupportedException` under trimming / Native AOT — which fails observably but disables the feature in an AOT host.
- **Expected shape:** keep JIT behavior unchanged; provide an AOT-safe path (for example a registered or source-generated `ITaskItem` → `TaskItem<T>` factory for the closed generic arguments a task actually declares) so typed task-item parameters can bind without `MakeGenericType` / expression compilation, replacing the observable-failure stopgap.
- **Deeper context:** [managing-trimming-and-aot.md §5.3](managing-trimming-and-aot.md#53-dynamic-code-runtime-code-generation).

## Backlog and non-goals

- **`Microsoft.Build.Tasks` as a fully trim/AOT-enabled assembly.** The Tasks assembly still has Backlog suppressions. One pending bucket is XML handling (`XmlSerializer`, `XslCompiledTransform`, `SignedXml`); other rows cover attribute reflection and assembly metadata. Some task entry points now fail gracefully under Native AOT with `RuntimeFeature.IsDynamicCodeSupported` guards, but the assembly still needs feature work before it can be treated as trimmable. The durable strategy is in [aot-trimming-strategy.md](aot-trimming-strategy.md), and the guard mechanics are in [managing-trimming-and-aot.md](managing-trimming-and-aot.md#53-dynamic-code-runtime-code-generation).
- **Removing public `ITaskFactory` RUC.** The public task-factory interface annotations are preview-era AOT annotations, not a permanent end-state. Registered and intrinsic tasks bypass the public interface members on the AOT-safe path; the remaining work is to prove the public interface call paths analyzer-clean, feature-gated, or observably unsupported, then remove the RUC before shipping the surface as stable. See [task-factory-aot.md](task-factory-aot.md).
- **Vetted false-positive suppressions.** The `TypeExtensions` helper suppressions and rooted-but-unreachable `Enum.GetValues(Type)` AOT suppressions remain valid false positives. They are tracked in [aot-trim-suppressions.md](aot-trim-suppressions.md).
