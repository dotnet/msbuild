---
applyTo: "src/MSBuild/**"
---

# MSBuild CLI Instructions

The `src/MSBuild/` folder contains the command-line entry point (`XMake.cs`), command-line parsing, server mode, and CLI-specific logic.

## CLI Switch Stability (Critical)

* **Never remove or rename existing CLI switches or aliases.** Build scripts worldwide depend on them. This is a hard rule with no exceptions.
* Properly deprecate switches before removal: emit a deprecation message for at least two major versions before considering removal.
* New switches must have clear, non-ambiguous names that do not conflict with existing switches or their abbreviations.
* Switch abbreviation rules must be preserved — existing shortest-unique prefixes must continue to work.

## XMake.cs Entry Point

* `XMake.cs` is the main entry point. Changes here affect every MSBuild invocation.
* Exit codes must remain stable — scripts check specific exit codes.
* Startup performance matters — avoid unnecessary initialization on the critical path.
* Error handling at the top level must catch and report all exceptions with actionable messages.

## Server Mode

* MSBuild server mode (`/nodeReuse`, build server) keeps processes alive between builds.
* State leaks between builds in server mode cause intermittent, hard-to-diagnose failures.
* Ensure all per-build state is properly reset between builds in server mode.
* See [threading spec](../../documentation/specs/threading.md) for concurrency constraints.

## Command-Line Parsing

* Parsing must be backward compatible — existing valid command lines must continue to work identically.
* Boolean switches can be specified as `-switch`, `-switch:true`, or `-switch:false`. Handle all forms.
* Response file (`@file`) processing must maintain ordering and nesting semantics.

## Backwards Compatibility of CLI Behavior

* Default verbosity, output format, and behavior must not change without a ChangeWave.
* The set of automatically-forwarded properties to child nodes must remain stable.
* Changes to how MSBuild discovers or loads projects (e.g., `.sln` vs `.slnx` handling) require careful compatibility analysis.

## Error Message Quality

* CLI-level errors (bad arguments, missing project files) must be immediately actionable.
* Use `ResourceUtilities` for all user-facing messages. Include the `/help` pointer when appropriate.
* Log startup diagnostics at `MessageImportance.Low` for debugging without cluttering normal output.

## Related Documentation

* [MSBuild Environment Variables](../../documentation/wiki/MSBuild-Environment-Variables.md)
* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [MSBuild apphost spec](../../documentation/specs/msbuild-apphost.md)
