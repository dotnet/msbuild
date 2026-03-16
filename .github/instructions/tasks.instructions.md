---
applyTo: "src/Tasks/**/*.cs"
---

# Built-in Tasks Instructions

Built-in tasks ship with MSBuild and cannot be independently versioned. Changes here affect every .NET build.

## Backwards Compatibility

* Built-in task behavior changes are breaking changes. Gate behavioral changes behind ChangeWave — see [ChangeWaves](../../documentation/wiki/ChangeWaves.md).
* Never remove or rename `[Output]` properties — downstream targets depend on them by name.
* Adding new `[Required]` properties is a breaking change for existing UsingTask declarations.
* New optional parameters should have sensible defaults that preserve existing behavior.

## Task Authoring Patterns

* Tasks must implement `ITask` (or extend `Task`/`ToolTask`). Use `TaskLoggingHelper` for logging, not `Console.WriteLine`.
* Validate inputs early in `Execute()` — fail fast with actionable error messages using `Log.LogError` with MSBxxxx codes.
* `[Output]` properties must be set before returning `true`. Callers depend on outputs being populated on success.
* Use `ResourceUtilities.FormatResourceStringStripCodeAndKeyword` for error formatting. All user-facing strings go in `.resx` files.

## ResolveAssemblyReference (RAR)

* RAR is the most complex built-in task. Changes require extensive testing across framework targeting scenarios.
* See [RAR documentation](../../documentation/wiki/ResolveAssemblyReference.md) and [RAR core scenarios](../../documentation/specs/rar-core-scenarios.md).
* RAR performance is critical — it runs for every project and can dominate build time.

## Path Handling

* Use `FileUtilities` helpers for path operations — do not roll custom path manipulation.
* Handle cross-platform path separators correctly. Never hardcode `\` or `/`.
* Support UNC paths and long paths (> 260 chars on Windows).
* File path comparisons must be OS-appropriate (case-insensitive on Windows, case-sensitive on Linux).

## Error Message Quality

* Error messages must state what happened, why, and what the user should do to fix it.
* Include file/line information when available via `Log.LogError(subcategory, code, ..., file, line, ...)`.
* Warnings must use correct severity — remember that new warnings break builds with `-WarnAsError`.

## Multithreaded Task Migration

* Tasks being migrated to support multithreaded MSBuild must not hold locks across yield points.
* Shared static state in tasks is a concurrency hazard in multi-process builds.

## Related Documentation

* [Contributing Tasks](../../documentation/wiki/Contributing-Tasks.md)
* [Tasks](../../documentation/wiki/Tasks.md)
* [Task Isolation](../../documentation/specs/task-isolation-and-dependencies.md)
