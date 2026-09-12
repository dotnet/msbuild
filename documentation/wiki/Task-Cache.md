# Task invocation caching

Enable the experimental persistent task cache explicitly:

```sh
dotnet msbuild -taskCache -buildCacheDirectory:/path/to/private/cache
```

`-taskCache:false` disables it. API hosts use `BuildParameters.TaskCache` and
`BuildParameters.BuildCacheDirectory`. Setting a directory alone enables nothing.
Without opt-in, ordinary task execution is unchanged.

## Configuration

The directory is captured once for the top-level build. Project properties and
environment variables do not configure it, and no directory property is injected
into projects. Relative command-line paths resolve against the invocation's
working directory. Whitespace configuration selects the default; the directory
switch requires a value.

| Platform | Default |
| --- | --- |
| Linux | `~/.cache/msbuild-cache` |
| macOS | `~/Library/Caches/msbuild-cache` |
| Windows | Local application-data directory, under `msbuild-cache` |

Storage uses BuildXL cache libraries with x64 native assets. Source-build and
unsupported architectures reject cache opt-in. Runtime validation is Linux-only;
see [storage details](../specs/task-cache-buildxl.md).

## Execution behavior

Normal target `Inputs`/`Outputs` timestamp checks run first. A skipped target
does not hash task inputs or access task results. Caching does not detect content
changes hidden by a successful target timestamp check.

For eligible tasks, lookup follows condition evaluation, batching, and parameter
binding. A hit restores declared files and typed task outputs instead of calling
`Execute()`. Current `<Output>` conditions and destinations apply normally.
Restored files receive fresh timestamps.

MSBuild resolves the requested task normally and checks that type's annotations;
it never substitutes tasks. Unannotated tasks run without participation warnings.
The mode cannot currently combine with `-question`, `-inputResultsCaches`, or
`-outputResultsCache`.

## Declaring a task contract

```csharp
[MSBuildDeclaredIOTask]
[MSBuildDeclaredIOInput(nameof(Sources))]
[MSBuildDeclaredIOOutput(nameof(DestinationFiles))]
public sealed class MyTask : Task
{
    // Task implementation and parameters.
}
```

The marker promises complete logical file I/O. Repeatable input/output attributes
name public instance parameters; attributes are not inherited, so every concrete
task must declare its contract. A marker without file declarations asserts no
logical file I/O.

`string` and `ITaskItem` describe one file path; arrays describe multiple paths.
Item metadata is not implicitly a file dependency. Strings are not globs or
semicolon-separated path lists. Relative paths use `TaskEnvironment.ProjectDirectory`,
normally the project directory rather than the imported targets directory.

Inputs and outputs must be disjoint. Output candidates may be a conservative
superset: the result records which exist and which are absent. Private scratch
files with no external effects are excluded; output parent-directory creation
is implicit. Directory-valued contracts are not supported.

Declarations must cover deterministic execution without unmodeled process,
build-engine, or filesystem effects. The engine validates shape and overlap,
not completeness: there is no sandbox or access monitor. Keys include contents
and absence, not all filesystem attributes. Tasks relying on other metadata need
additional modeling.

### Task output values

`[Output]` marks returned values, not physical file writes. The cache records
each parameter referenced by an `<Output>` element, including item metadata.
These getters must be safe to read after successful execution even when a binding
condition is false. Each is read once before cleanup; publication follows cleanup.

Referenced parameter names enter the key. Destination property/item names and
binding conditions do not. Each binding receives an independent restored value.
Unreferenced output getters are not read.

### Optional mode awareness

When caching is enabled, the engine supplies `MSBuildTaskCacheEnabled=true`
before file-based evaluation. Explicit `-taskCache:false` supplies false.
The property alone does not enable engine caching.

A task need not expose a parameter of that name. If it does expose a Boolean,
an explicit true binding is required for that invocation to participate.
Hosts with already-evaluated projects must provide any mode globals needed by
their targets during evaluation.

## Warnings and failures

Successful executions can cache exact `BuildWarningEventArgs` warnings from
execution and referenced output getters. Replay applies the current warning
policy and task context. A warning cached as a message can become an error on
a later hit; `-warnNotAsError` applies anew.

Initialization, binding, input-getter, cleanup, and unsupported warning-subclass
diagnostics prevent caching instead of being replayed twice or incompletely.
Raw errors, failed tasks, exceptions, and cancellation prevent publication,
including errors demoted by `ContinueOnError`. Ordinary messages are not replayed.

An otherwise successful task can be cached even when warning promotion fails the
overall build; replay under that policy still fails. Invalid declarations remain
subject to normal warning policy.

Missing entries or evicted content are misses. Corruption and operational
failures fail the build, not fall back to executing the task.
Restoration validates/stages all artifacts, then completes destination replacement
without cancellation interruption. Filesystem errors can leave partial outputs:
fix the cause and clean/rebuild. There is no multi-file rollback or guarantee that
an ordinary incremental build will repair the state.

## Storage lifetime

Results survive process exit. One coordinator owns the cache for the top-level
build; workers share it through existing MSBuild communication. Another build
using the same root fails immediately. Pins last for the build.

Keep storage private: manifests can contain task values and warnings, and blobs
contain build outputs. Keys include absolute paths and the effective environment,
so moved checkouts or changing environment values can prevent reuse.
Stop builds before removing the cache.

See the [design decisions](../specs/task-cache-decisions.md) and
[manifest format](../specs/task-cache.md).
