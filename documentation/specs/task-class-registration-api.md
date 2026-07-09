# API Proposal: Host registration of task classes

**Status:** Implemented as the static `Microsoft.Build.Utilities.Task.RegisterTask` overloads
([Task.cs](../../src/Utilities/Task.cs)). The API was placed on `Task` rather than a new `TaskClassRegistry`
type, and the engine now constructs, binds, and executes a registered task with the reflective
task-execution path disabled - so a trimmed/Native AOT host can build a project whose tasks are registered.
Companion to [task-parameter-type-registration-api.md](task-parameter-type-registration-api.md).

## Background and motivation

To execute a task, MSBuild reflects over the task type end to end: it loads the task assembly, resolves
the task `Type`, constructs an instance (via the task factory / `Activator.CreateInstance`), binds the
declared parameters onto its properties by reflection, calls `Execute()`, and reads `[Output]` properties
back - all in
[`TaskExecutionHost`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs). This entire
path is reflective and is gated by the
[`EnableReflectiveTaskExecution`](../../src/Framework/FeatureSwitches.cs) feature switch. Under a
trimmed / Native AOT host the switch is substituted `false`: the reflective instantiation path is
removed and reaching task execution fails observably (the engine reports an error rather than crashing in
reflection).

The consequence is that a trimmed/AOT host can **evaluate** projects but cannot **run tasks**. Closing
that gap needs a reflection-free way to (a) instantiate a task by its registered name and (b) bind its
parameters. This proposal covers (a) and the registration seam; (b) builds directly on the parameter
**type** registry already implemented in
[task-parameter-type-registration-api.md](task-parameter-type-registration-api.md).

A host that runs the engine in-process and is itself trimmed/AOT-compiled (the .NET SDK CLI) already
references the task assemblies it cares about statically. It needs to hand MSBuild a pre-constructed way
to make those tasks, the same shape as [`SdkResolver.Register`](sdk-resolver-host-registration-api.md)
and the task **parameter type** registry.

## API Proposal

Two static methods on the public `Microsoft.Build.Utilities.Task` (the base class task authors derive from),
mirroring the host-registration shape of [`SdkResolver.Register`](sdk-resolver-host-registration-api.md) and
the task **parameter type** registry on `TaskItem`:

```diff
 namespace Microsoft.Build.Utilities;

 public abstract class Task : ITask
 {
     // ... existing members ...

+    /// <summary>
+    /// Registers a task type under the name a target uses to invoke it (the TaskName of a <UsingTask>), so
+    /// MSBuild can instantiate and run it without loading its assembly or resolving its type by reflection.
+    /// The [DynamicallyAccessedMembers] roots the type's public constructor and properties so construction
+    /// and parameter binding stay trim-safe.
+    /// </summary>
+    public static void RegisterTask<
+        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
+        string taskName) where T : ITask, new();
+
+    /// <summary>
+    /// Registers a task under the given name with an explicit factory, so construction is fully
+    /// reflection-free (the host supplies the constructor). The host is responsible for preserving the
+    /// task type's public properties under trimming (parameter binding still reflects over them).
+    /// </summary>
+    public static void RegisterTask(string taskName, Func<ITask> factory);
 }
```

The generic overload is the trim-safe primitive (its `[DynamicallyAccessedMembers]` roots the type, and
construction is `new T()`); the `Func<ITask>` overload is the convenience for tasks without a public
parameterless constructor or that need custom construction. Both forward to an internal
[`TaskClassRegistry`](../../src/Framework/TaskClassRegistry.cs) in Microsoft.Build.Framework - the lowest
assembly - so the engine, the task library, and the public surface all reach it. Placing the methods on the
existing public `Task` keeps the surface minimal, the same trade-off the parameter-type registry made by
hanging its methods off `TaskItem`.

### Pre-registering the common MSBuild tasks

The proposal includes pre-registering the frequently used built-in tasks (for example `Message`, `Copy`,
`MakeDir`, `RemoveDir`, `WriteLinesToFile`, `ReadLinesFromFile`, `Delete`, `Touch`, `Error`, `Warning`),
so a stock build runs the common tasks under AOT with no host action. These tasks live in
**Microsoft.Build.Tasks.Core**, which the engine (`Microsoft.Build`) must not reference (layering). So
pre-registration is published *from the Tasks assembly*, by one of:

- a `[ModuleInitializer]` in Microsoft.Build.Tasks.Core that registers its common tasks when the assembly
  is loaded, or
- an explicit `public static void Microsoft.Build.Tasks.BuiltInTasks.RegisterAll()` a host calls during
  startup (more explicit, no load-order surprises).

The curated set, and whether `ToolTask`-based tasks (Csc/Vbc, which shell out) belong in it, are open
questions for review.

## Semantics

- **Registry first, reflection-free.** When a task name is registered, the engine constructs it from the
  registration (factory or rooted type) and binds parameters without loading the task assembly by path or
  reflecting to discover the type. This is the path that works with `EnableReflectiveTaskExecution`
  effectively satisfied for that task.
- **Precedence and task identity.** The registry is consulted before the project's `<UsingTask>` table, so
  a registered name takes precedence over a same-named `<UsingTask>`. Registration is by name only - it does
  not participate in `Runtime`/`Architecture` task-identity selection (the registered task is always the one
  constructed), so registering a name collapses any task-identity variants of it. For stock builds nothing is
  registered, so existing `<UsingTask>` resolution and task-identity selection are unchanged.
- **Fallback / observable failure.** An unregistered task name falls back to the existing reflective load
  under the JIT, and fails observably under AOT (the current `EnableReflectiveTaskExecution`-off
  behavior), unchanged.
- **Intrinsic tasks stay available.** The engine-internal `MSBuild` and `CallTarget` tasks are resolved
  from known engine types (via `IntrinsicTaskFactory`) with no assembly probing or by-name type
  resolution, so they run with `EnableReflectiveTaskExecution` off and need no registration. Because
  virtually every real build dispatches through `<MSBuild>` and `<CallTarget>`, treating them as
  always-available (rather than requiring the host to register them) keeps the registered/AOT path usable
  for real project graphs.
- **Parameter binding depends on the type registry.** Binding a `<ParameterGroup>`-typed or
  reflected-property parameter still needs the parameter's *type* to be resolvable; that is exactly what
  [task-parameter-type-registration-api.md](task-parameter-type-registration-api.md) provides. The two
  registries are designed to be used together.
- **Process scope and timing.** Process-global, established once at host startup, mirroring the SDK
  resolver and parameter-type registries.

## API Usage

```csharp
using Microsoft.Build.Utilities;

// A host bakes in the tasks it supports under AOT, before the first build.
Task.RegisterTask<Microsoft.Build.Tasks.Message>("Message");
Task.RegisterTask<Microsoft.Build.Tasks.Copy>("Copy");

// Or with an explicit factory (construction is reflection-free; the host roots the type for binding):
Task.RegisterTask("MyTask", () => new MyNamespace.MyTask());

// The common built-ins in one call (published from the Tasks assembly):
Microsoft.Build.Tasks.BuiltInTasks.RegisterAll();
```

## Alternative designs

1. **Static methods on `Microsoft.Build.Utilities.Task`** (the base class most task authors derive from)
   instead of a new `TaskClassRegistry` type - discoverable from the type authors already use, mirroring
   how the parameter-type registry hangs off `TaskItem`. Trade-off: `Utilities` is above the engine, so
   the backing store would still live in Framework with `Utilities` forwarding (as the parameter-type
   registry does).
2. **A source generator** ([task-factory-aot.md](../aot/task-factory-aot.md)) that emits the registrations and
   strongly-typed parameter setters from the task classes a host references. The generator is the
   declarative counterpart; this imperative `RegisterTask` is the primitive it would target, and the
   manual API is what hosts use until/unless the generator ships.
3. **Factory-only (no generic overload).** Smallest, most explicit, fully reflection-free surface; the
   generic overload is sugar that relies on rooting. Could ship factory-only first.
4. **Per-`ProjectCollection` registration** instead of process-global - more precise lifetime, larger
   change; not required by the motivating in-process-host scenario.

## Risks

- **Parameter binding, not construction, is the hard part under AOT.** Constructing a registered task is
  easy; setting its parameters today goes through reflection over the task's properties. Rooting the
  properties (the `[DynamicallyAccessedMembers]` above) makes reflective set work but at a size cost; a
  generated binder (alternative 2) avoids reflection entirely and is the longer-term answer. This is the
  main reason the proposal is staged after the parameter-**type** registry.
- **Layering for the built-in pre-registration.** The engine cannot reference Microsoft.Build.Tasks.Core,
  so the built-in set must be registered from the Tasks assembly (module initializer or explicit call),
  which introduces a startup-ordering contract.
- **Output parameters and item conversion.** Reading `[Output]` properties back and converting to
  `ITaskItem`/value types reuses the same reflection-sensitive machinery; it must be made trim-safe
  alongside, again leaning on the parameter-type registry.
- **Process-global mutable static / test isolation.** As with the other registries; a test-only reset and
  "register at startup" guidance apply.

## Related

- [task-parameter-type-registration-api.md](task-parameter-type-registration-api.md) - the implemented companion (registers parameter **types**); required for binding.
- [sdk-resolver-host-registration-api.md](sdk-resolver-host-registration-api.md) - the host-registration shape this mirrors.
- [task-factory-aot.md](../aot/task-factory-aot.md) - the source-generator direction this would complement.
- [EnableReflectiveTaskExecution](../../src/Framework/FeatureSwitches.cs) - the switch that gates today's reflective task execution and fails observably under AOT.

## Implementation status

Implemented in this change:

- **API.** `RegisterTask<T>(string)` and `RegisterTask(string, Func<ITask>)` on
  [`Microsoft.Build.Utilities.Task`](../../src/Utilities/Task.cs), forwarding to the internal
  [`TaskClassRegistry`](../../src/Framework/TaskClassRegistry.cs) (a name -> registration map; each
  [`TaskClassRegistration`](../../src/Framework/TaskClassRegistration.cs) holds a `Func<ITask>` and a
  `LoadedType` built once, eagerly for the generic overload where the type's `[DynamicallyAccessedMembers]`
  is in scope).
- **Engine wiring.** [`TaskExecutionHost`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs)
  consults the registry first in `FindTask` (building a `TaskFactoryWrapper` from a new
  [`RegisteredTaskFactory`](../../src/Build/Instance/TaskFactories/RegisteredTaskFactory.cs) and the
  registered `LoadedType`), constructs a registered task with a non-interface, reflection-free method
  (avoiding the `[RequiresUnreferencedCode]` `ITaskFactory.CreateTask`), and binds its parameters with the
  `Type.GetType`-free `ResolveTaskParameterType` (the live property type in-proc; the assembly-qualified
  fallback is gated behind `EnableReflectiveTaskExecution`). A registered task runs with that switch off.
  The intrinsic `MSBuild` and `CallTarget` tasks are resolved on the same switch-off path
  (`TryCreateIntrinsicTaskFactory` building an `IntrinsicTaskFactory`, instantiated by
  `IntrinsicTaskFactory.CreateIntrinsicTask` - a direct `new` with no reflection), so real builds that
  dispatch through `<MSBuild>`/`<CallTarget>` run with the switch off too.
- **Built-ins.** [`Microsoft.Build.Tasks.BuiltInTasks.RegisterAll`](../../src/Tasks/BuiltInTasks.cs)
  registers the common tasks (`Message`, `Warning`, `Error`, `MakeDir`, `RemoveDir`, `Copy`, `Delete`,
  `Touch`, `WriteLinesToFile`, `ReadLinesFromFile`) from the Tasks assembly (the engine cannot reference it).
- **Build-path AOT enablement.** Running a build in-process pulls in the build-execution path the prior AOT
  work scoped out. Making it trim-clean for registered-task builds added a feature switch
  [`EnableReflectiveLoggerLoading`](../../src/Framework/FeatureSwitches.cs) that gates the reflective
  `LoggerDescription.CreateForwardingLogger` calls in
  [`LoggingService`](../../src/Build/BackEnd/Components/Logging/LoggingService.cs) (so the trimmer drops
  `TypeLoader`/`MetadataLoadContext`), read the handshake file version from `AssemblyFileVersionAttribute`
  instead of `Assembly.Location` ([`Handshake`](../../src/Framework/BackEnd/Handshake.cs)), and guarded
  the remaining single-file `Assembly.Location` reads on `RuntimeFeature.IsDynamicCodeSupported`
  ([`LoadedType`](../../src/Framework/Loader/LoadedType.cs),
  [`CoreClrAssemblyLoader`](../../src/Framework/Loader/CoreCLRAssemblyLoader.cs)). Loggers supplied as
  `ILogger` instances are the supported way to log under AOT.
- **Tests.** [`RegisteredTaskAotTests`](../../src/aot-validation/RegisteredTaskAotTests.cs) builds a
  hand-authored project end to end under Native AOT - the built-in tasks and a host-registered custom task
  (with an `[Output]` bound back) run, with real file side effects - and an unregistered task fails
  observably. [`DotnetTemplateAotTests`](../../src/aot-validation/DotnetTemplateAotTests.cs) builds the
  real `dotnet new` `console`/`classlib` templates under AOT: evaluation and the registered built-in tasks
  run, then the build fails observably at the first task from the SDK's own task assembly
  (`Microsoft.NET.Build.Tasks`, which is not part of `Microsoft.Build.Tasks.Core` and cannot be registered)
  - degrading to a reported error rather than a reflection crash. The harness AOT-publishes warning-clean
  and passes under Native AOT.

Not done (future work): a source-generated parameter binder (alternative 2) to remove the rooted-reflection
parameter binding; fully AOT-cleaning the rest of the `BuildManager` surface (the build entry points remain
`[RequiresUnreferencedCode]` for reflective logger/plugin loading - an in-process host that passes
`ILogger` instances and no project-cache plugins, like the harness, does not hit that path).
