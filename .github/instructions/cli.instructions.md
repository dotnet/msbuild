---
applyTo: "src/MSBuild/**"
---

# MSBuild CLI Instructions

Command-line entry point (`XMake.cs`), argument parsing, server mode, and CLI-specific logic.

## CLI Switch Stability (Critical)

* **Never remove or rename existing switches or aliases** — build scripts worldwide depend on them.
* New switches must not conflict with existing switches or their abbreviations.
* Switch abbreviation rules must be preserved — existing shortest-unique prefixes must continue to work.

## XMake.cs Entry Point

* Exit codes must remain stable — scripts check specific exit codes.
* Startup performance matters — avoid unnecessary initialization on the critical path.
* Top-level error handling must catch and report all exceptions with actionable messages.

## Server Mode

* Server mode keeps processes alive between builds — state leaks cause intermittent failures.
* Ensure all per-build state is properly reset between builds.
* See [threading spec](../../documentation/specs/threading.md) for concurrency constraints.

## Command-Line Parsing

* Backward compatible — existing valid command lines must work identically.
* Boolean switches: `-switch`, `-switch:true`, `-switch:false` — handle all forms.
* Response file (`@file`) processing must maintain ordering and nesting semantics.

## CLI Behavior Compatibility

* Default verbosity, output format, and behavior must not change without a [ChangeWave](../../documentation/wiki/ChangeWaves.md).
* The set of automatically-forwarded properties to child nodes must remain stable.
* Changes to project discovery (`.sln` vs `.slnx` handling) require careful compatibility analysis.

## Error Messages

* CLI-level errors (bad arguments, missing project files) must be immediately actionable.
* Include the `/help` pointer when appropriate.

## Related Documentation

* [MSBuild Environment Variables](../../documentation/wiki/MSBuild-Environment-Variables.md)
* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [MSBuild apphost spec](../../documentation/specs/msbuild-apphost.md)
