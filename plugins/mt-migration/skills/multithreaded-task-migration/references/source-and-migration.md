# Source preflight and migration mechanics

Use this reference for task eligibility, construction, environment plumbing, and the decision to migrate. These are implementation-sensitive contracts, not a promise that every host or package version behaves identically.

## Resolve the authoring and execution context

Record the task source revision and the Framework/Utilities assemblies it consumes. Distinguish the task-authoring repository from a consuming project and from an installed plugin cache.

Read the task and test projects plus their imported configuration. In an MSBuild checkout, useful starting points are `global.json`, `Directory.Build.props`, and `src\Directory.Build.props`. Derive supported TFMs, SDK/runner requirements, analyzer versions, and existing helpers from those files; do not install or upgrade anything just to make a recipe fit.

Establish how the task is created: ordinary `UsingTask`, explicit task-host factory, custom factory, host-registered task, or direct `new`. Record the actual MT mode and any architecture/runtime or out-of-process request. The annotation is not proof of the selected host.

## Attribute and interface are not interchangeable

In the current MSBuild implementation:

- `TaskRouter.NeedsTaskHostInMultiThreadedMode` uses the concrete task's `MSBuildMultiThreadableTaskAttribute`, not `IMultiThreadableTask`, for its MT eligibility decision.
- Attribute lookup is non-inheriting and checks the namespace/name. An attribute polyfill must preserve the full name and non-inheritable semantics; verify that the target host supports that route.
- `IMultiThreadableTask` supplies the environment property. It can be implemented by a shared base without opting every derived task into shared-process execution.
- `ToolTask` already implements the interface and supplies a virtual `TaskEnvironment` property. Do not hide it with a second property in a derived task.
- A task that needs no environment APIs may need only the concrete-class attribute. That still changes its eligibility for hosting and warrants review of state reached through bases and helpers.

The router is explicitly for MT mode. Its callers handle explicit host requests and already-out-of-process cases. Do not generalize its result to every factory, runtime, architecture, or traditional multiprocess build.

## Construction happens before ordinary property injection

The normal task execution host constructs the task, assigns `BuildEngine` and `HostObject`, and then assigns `IMultiThreadableTask.TaskEnvironment` before execution. Constructor and property-initializer work can therefore run too early to use that ordinary injection.

Where a task owns the property, a usable direct-construction default can be:

```csharp
public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
```

This prevents a null environment; it does **not** provide a project-isolated MT environment. Do not overwrite the engine-supplied property in `Execute`.

The current normal instantiation path also supports a public constructor taking `TaskEnvironment`, preferred when available. A constructor can assign the supplied environment and compute environment-dependent defaults from it. Property initializers still run before the constructor body: an initializer reading process CWD or `TaskEnvironment.Fallback` is not fixed merely by adding that constructor.

For example, an environment-dependent default belongs after the assignment:

```csharp
public MyTask(TaskEnvironment taskEnvironment)
{
    TaskEnvironment = taskEnvironment;
    DefaultDirectory = taskEnvironment.ProjectDirectory.Value;
}
```

This is a member sketch, not an instruction to replace every constructor. Preserve a parameterless constructor where supported consumers require it. Verify custom factories, separate-AppDomain paths, host-registered construction, and directly constructed tasks separately; they need not use the normal reflection-based path.

## Understand the selected environment driver

`TaskEnvironment.Fallback` delegates to the multiprocess driver. It reads process CWD/environment and can modify that process state. It is not an immutable snapshot or an isolation mechanism.

The MT driver maintains a virtual directory and environment dictionary. In the inspected implementation, its directory setter also updates a thread-local path-resolution context. Its documentation assumes single-threaded use of a driver by a thread node. Environment instances and lifetimes are engine-owned; do not assume a fresh independent driver for every task object or retain one for later unrelated tasks.

If a task starts internal workers, obtain the required immutable input values before dispatch or use appropriate synchronization/lifetime management. Do not hand mutable engine-owned environment state to workers that outlive the invocation.

Use `GetEnvironmentVariable`, `SetEnvironmentVariable`, and `GetProcessStartInfo` where their semantics match the task's contract. The current MT setter updates the virtual dictionary; it does not categorically reject all engine-looking variable names. Conversely, changing a virtual variable does not prove that an engine cache or already-initialized subsystem will observe it.

For variables consumed by the engine, SDK discovery, or child tools, trace **who reads the value and when**. Pass child-specific overrides to the actual child start information when appropriate. Verify additions, replacements, removals, and inherited values; do not assume either complete environment replacement or universal immutability.

## Migrate filesystem boundaries without changing the public contract

Use the task's environment to resolve project-relative filesystem inputs. Retain the original representation for outputs and diagnostics when that is the existing contract.

```csharp
AbsolutePath input = TaskEnvironment.GetAbsolutePath(InputFile);
string text = File.ReadAllText(input);
```

Resolve once at the earliest **semantically valid** boundary, after existing optional-input guards and within the required exception-handling scope. Do not eagerly resolve an unused optional path simply to satisfy an API checklist.

Prefer `AbsolutePath` through internal path-processing helpers where preserving `Value` and `OriginalValue` is useful. Avoid pairs of unrelated strings that can drift apart. This is not permission to change public task property types, public helper signatures, or overload resolution without compatibility analysis.

If the host already binds a typed path input, inspect that binding rather than resolving it again. If a helper computes a new path, preserve the original form of that new value separately from its I/O form.

Do not mechanically duplicate the implementation into MT/non-MT branches. The environment abstraction already selects a driver. Where an intentional mode/host difference is necessary, establish the contract and its coverage rather than forbidding every conditional.

## Directly constructed and nested tasks

A plain `new ChildTask()` does not trigger normal engine injection. Inspect what the constructor or base supplies: it may be fallback, explicitly injected state, or an uninitialized property.

When a parent intends a child to share its task execution context, supply that context through the child's supported constructor/property **before environment-dependent work**. Property assignment after construction cannot repair defaults already computed by the constructor.

Also preserve the child's required `BuildEngine`, logging, host object, parameter setup, and lifecycle. Some wrappers already perform this work; direct construction is a boundary to audit, not automatically a bug or a meaningless migration.

Distinguish host-registered task factories from registered task **objects**. The former create tasks; `IBuildEngine4` object registration is a cache/lifetime facility.

## Not migrating can be the correct decision

Do not add the attribute when the reachable task contract requires unsafe process-global behavior or an unresolved race has material impact. Leaving it unannotated retains the router's isolation choice where that router applies.

That does not prove exactly-once execution across a build, isolation between every worker process, or preservation of unrelated changes already made in the migration. Explain the concrete constraint, supported hosts, and remaining behavior. A reasoned non-migration outcome is not automatic approval of the whole diff.

## Current-source landmarks

These are paths to resolve in the **reviewed checkout**, not files bundled into this portable plugin:

| Contract | MSBuild source / symbol |
| --- | --- |
| Attribute identity and inheritance | `src\Framework\MSBuildMultiThreadableTaskAttribute.cs`; `src\Build\BackEnd\Components\RequestBuilder\TaskRouter.cs` |
| Environment API and driver behavior | `src\Framework\TaskEnvironment.cs`, `MultiProcessTaskEnvironmentDriver.cs`, `MultiThreadedTaskEnvironmentDriver.cs` |
| Property/constructor contract | `src\Framework\IMultiThreadableTask.cs`; `src\Shared\TaskLoader.cs` |
| Post-construction injection | `src\Build\BackEnd\TaskExecutionHost\TaskExecutionHost.cs`, `InitializeForBatch` |
| Host-registered construction | `src\Build\Instance\TaskFactories\RegisteredTaskFactory.cs` |
| Existing defaults | `src\Utilities\ToolTask.cs`; the concrete task and its bases |
