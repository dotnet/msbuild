---
applyTo: "src/Build/Logging/**"
---

# Logging Infrastructure Instructions

Binary logger, console loggers, terminal logger, and event forwarding infrastructure.

## Binary Log Format Stability

* `.binlog` format must maintain backward compatibility — older readers must handle newer logs.
* `BuildEventArgsWriter.cs`/`BuildEventArgsReader.cs` are the serialization boundary — field additions must be versioned.
* Always write new fields at the end of existing records. Readers must handle missing fields.
* Test round-trip: write → read → compare for all modified event types.

## Terminal Logger (FancyLogger)

* Must handle terminal width changes, very narrow terminals, and non-TTY output (piped to file).
* Concurrent project builds must render without corruption.
* ANSI escape sequences must be cross-platform compatible.

## Console Logger

* `ParallelConsoleLogger.cs` handles multi-project console output.
* `MessageImportance` filtering: `High` always shows, `Normal` at normal verbosity, `Low` at detailed.
* Never change the default output format without a [ChangeWave](../../documentation/wiki/ChangeWaves.md) — build log parsers depend on it.

## Build Event Handling

* Event forwarding between nodes must preserve ordering and completeness.
* Central loggers see all events; distributed loggers see only their node's events. See [Logging Internals](../../documentation/wiki/Logging-Internals.md).

## Diagnostics Completeness

* Behavioral changes must produce corresponding binary log entries.
* Error/warning events must include file, line, and column when available.
* Prefer structured events over string messages for programmatic consumption.

## Related Documentation

* [Binary Log](../../documentation/wiki/Binary-Log.md)
* [Logging Internals](../../documentation/wiki/Logging-Internals.md)
* [Providing Binary Logs](../../documentation/wiki/Providing-Binary-Logs.md)
