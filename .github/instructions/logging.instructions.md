---
applyTo: "src/Build/Logging/**"
---

# Logging Infrastructure Instructions

The logging subsystem (180 review comments) includes the binary logger, console loggers, terminal (fancy) logger, and event forwarding infrastructure.

## Binary Log Format Stability

* The `.binlog` format must maintain backward compatibility — older readers must handle logs from newer MSBuild versions gracefully.
* `BuildEventArgsWriter.cs` / `BuildEventArgsReader.cs` are the serialization boundary. Any field additions must be versioned.
* Always write new fields at the end of existing records. Readers must handle missing fields for forward compat.
* Test round-trip: write → read → compare for all modified event types.

## Terminal Logger (FancyLogger)

* `FancyLogger.cs` (22 comments) renders live build progress. It must handle:
  - Terminal width changes and very narrow terminals.
  - Non-TTY output (piped to file) — graceful fallback required.
  - Concurrent project builds rendering without corruption.
* ANSI escape sequence usage must be cross-platform compatible.
* Output must be visually correct — test with actual terminal rendering, not just string comparison.

## Console Logger

* `ParallelConsoleLogger.cs` (32 comments — most-reviewed logging file) handles multi-project console output.
* Message importance filtering must be consistent: `MessageImportance.High` always shows, `Normal` at normal verbosity, `Low` at detailed, `Diagnostic` only at diagnostic.
* Never change the default output format without a ChangeWave — build log parsers depend on it.

## Log Message Formatting

* All user-facing messages must go through resource strings (`.resx`), never hardcoded.
* Use `ResourceUtilities.FormatResourceStringStripCodeAndKeyword` for proper MSBxxxx code extraction.
* Log messages must include sufficient context for debugging without the binary log.

## Build Event Handling

* Event forwarding between nodes must preserve ordering and completeness.
* Central vs distributed logger distinction matters — central loggers see all events, distributed loggers see only their node's events.
* See [Logging Internals](../../documentation/wiki/Logging-Internals.md) for the forwarding architecture.

## Diagnostics Completeness

* All behavioral changes must produce corresponding binary log entries.
* Error/warning events must include file, line, and column when available.
* Structured events are preferred over string messages for programmatic consumption.

## Related Documentation

* [Binary Log](../../documentation/wiki/Binary-Log.md)
* [Logging Internals](../../documentation/wiki/Logging-Internals.md)
* [Providing Binary Logs](../../documentation/wiki/Providing-Binary-Logs.md)
