---
applyTo: "src/Tasks/**/*.cs"
---

# Built-in Tasks Instructions

Built-in tasks ship with MSBuild and cannot be independently versioned.

## Backwards Compatibility

* Gate behavioral changes behind a [ChangeWave](../../documentation/wiki/ChangeWaves.md).
* Never remove or rename `[Output]` properties — downstream targets depend on them by name.
* Adding new `[Required]` properties is a breaking change for existing `UsingTask` declarations.
* New optional parameters must default to preserving existing behavior.

## Task Authoring Patterns

* Extend `Task` or `ToolTask`. Use `TaskLoggingHelper` for logging, not `Console.WriteLine`.
* Validate inputs early in `Execute()` — fail fast with `Log.LogError` using MSBxxxx codes.
* `[Output]` properties must be set before returning `true`.
* All user-facing strings go in `.resx` files; use `ResourceUtilities.FormatResourceStringStripCodeAndKeyword` for formatting.

## ResolveAssemblyReference (RAR)

* Most complex built-in task — changes require extensive testing across framework targeting scenarios.
* RAR performance is critical — it runs for every project and can dominate build time.
* See [RAR docs](../../documentation/wiki/ResolveAssemblyReference.md) and [core scenarios](../../documentation/specs/rar-core-scenarios.md).

## Path Handling

* Use `FileUtilities` helpers — do not roll custom path manipulation.
* Support UNC paths, long paths (> 260 chars), and cross-platform separators.

## Multithreaded Task Migration

* All built-in tasks implement `IMultiThreadableTask` with a default `TaskEnvironment` backed by `MultiProcessTaskEnvironmentDriver.Instance`.
* Shared static state is a concurrency hazard in multi-process builds.

## Related Documentation

* [Contributing Tasks](../../documentation/wiki/Contributing-Tasks.md)
* [Tasks](../../documentation/wiki/Tasks.md)
* [Task Isolation](../../documentation/specs/task-isolation-and-dependencies.md)
