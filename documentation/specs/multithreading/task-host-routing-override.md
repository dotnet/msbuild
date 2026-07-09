# UsingTask `TaskHostRouting` attribute (prototype)

> **Status: prototype.** This document describes an experimental knob for overriding the
> multi-threaded task-routing decision from project XML. It is being prototyped to inform the
> design discussion on [dotnet/msbuild#13738](https://github.com/dotnet/msbuild/issues/13738).

## Motivation

In multi-threaded mode MSBuild routes each task to one of three execution locations:

- **In-process** in the thread node (for tasks known to be thread-safe), or
- a **sidecar** TaskHost process (long-lived, reused across invocations), or
- a **transient** TaskHost process (terminates after each invocation).

Today the routing decision is made entirely by the engine: tasks marked with
`MSBuildMultiThreadableTaskAttribute` run in-process, everything else runs in a sidecar, and a
hard-coded engine list (currently only `NuGet.Build.Tasks.RestoreTask`) forces a transient host.

A **build/repo engineer** sometimes needs to override this per task without changing the task
assembly — for example to pin a task they trust to the in-proc path for performance, or to isolate
a task that misbehaves in a reused host. This is the XML counterpart to the environment-variable
escape hatch; it is aimed at the person authoring the build, targets the same three locations, and
travels with the project rather than the shell environment.

## Design

A new optional attribute `TaskHostRouting` is added to the `UsingTask` element, analogous to the
existing `Runtime` and `Architecture` attributes.

```xml
<UsingTask TaskName="My.Trusted.Task"
           AssemblyFile="MyTasks.dll"
           TaskHostRouting="InProc" />

<UsingTask TaskName="Some.Legacy.Task"
           AssemblyFile="Legacy.dll"
           TaskHostRouting="Transient" />
```

### Allowed values

| Value | Effect in multi-threaded mode |
| --- | --- |
| `InProc` | Force the task in-process within the thread node. Asserts the task is thread-safe. |
| `Sidecar` | Force the task into a reusable (sidecar) TaskHost process. |
| `Transient` | Force the task into a transient TaskHost that terminates after execution. |
| *(unset)* | No override; the engine applies its default routing decision. |

Values are matched case-insensitively. Any other value is rejected during evaluation with the
standard `InvalidAttributeValue` diagnostic. The override only has an effect in multi-threaded mode;
in single-proc / multi-proc builds it is inert.

### Precedence vs. engine routing

`TaskHostRouting` takes precedence over the attribute/interface-based engine decision:

- `InProc` runs the task in-process even if it lacks `MSBuildMultiThreadableTaskAttribute`.
- `Sidecar` / `Transient` run the task out of process even if it is marked thread-safe.
- `Transient` also overrides the sidecar-vs-transient choice, giving a fresh process per invocation
  (the same mechanism used for the built-in `RestoreTask` workaround).

### Interaction with override `UsingTask`

Because `TaskHostRouting` is an ordinary `UsingTask` attribute, the existing override-`UsingTask`
mechanism applies unchanged: a later (or `Override="true"`) `UsingTask` registration for the same
task name wins, including its `TaskHostRouting` value. This lets a repo-level `.props`/`.targets`
pin routing for a task defined elsewhere.

## Implementation notes

- `XMakeAttributes` defines the attribute name, the allowed value set, and
  `IsValidTaskHostRoutingValue`.
- `ProjectParser` accepts the attribute on `UsingTask`; `ProjectUsingTaskElement` exposes it as the
  `TaskHostRouting` property.
- `TaskRegistry` expands and validates the value, stores it on the `RegisteredTaskRecord` (and
  serializes it so it survives out-of-proc node marshaling), and passes it to
  `AssemblyTaskFactory.InitializeFactory`.
- `AssemblyTaskFactory` parses it into a `TaskHostRoutingOverride` (via `TaskRouter.ParseRoutingOverride`)
  and applies it in `CreateTaskInstance`, alongside the existing routing logic.

## Open questions

1. Attribute/value naming (`TaskHostRouting` vs. e.g. `Threading`/`ExecutionLocation`; `InProc` vs.
   `InProcess`).
2. Whether `InProc` should hard-fail (vs. silently fall back to a host) when the assembly could not
   be loaded in-process for another reason (e.g. a `Runtime`/`Architecture` mismatch).
3. Whether to keep the environment-variable escape hatch as a testing convenience alongside this XML
   knob (see #13738).
