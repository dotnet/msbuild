# Binary-log event content and collection

## Capture is conditional

[BinaryLogger](../../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs) defaults to diagnostic verbosity and requests detailed data such as evaluation properties/items and target outputs. It cannot recover events a producer did not emit, information a transport dropped, or arbitrary custom fields its writer does not support.

Examples of intentional limits:

- [Common props](../../../../src/Tasks/Microsoft.Common.props) suppress selected verbose task parameters/metadata by default.
- [PropertyTrackingEvaluatorDataWrapper](../../../../src/Build/Evaluation/PropertyTrackingEvaluatorDataWrapper.cs) makes structured property tracking conditional and skips costly/redundant cases.
- [BuildEventArgsWriter](../../../../src/Build/Logging/BinaryLogger/BuildEventArgsWriter.cs) filters environment information and falls back for unknown event shapes.
- [TaskExecutionHost](../../../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) checks task-parameter logging and critical-event settings.

Do not add expensive logging merely to satisfy "the binlog captures everything." Preserve appropriate observability, but consider emission cost, size, privacy, and the intended diagnostic audience.

## Importance is not severity or verbosity

[MessageImportance](../../../../src/Framework/BuildMessageEventArgs.cs) defines three levels:

| Importance | Typical console threshold / use |
|---|---|
| High | Minimal and above; important user-facing status |
| Normal | Normal and above; ordinary progress |
| Low | Detailed and above; diagnostic detail |

These are typical console mappings; another logger can present events differently. There is no `MessageImportance.Diagnostic`. Diagnostic is a `LoggerVerbosity` level.

Warnings/errors have their own event contracts, not "High importance" equivalents. Use [diagnostic authoring](../../authoring-errors-and-warnings/SKILL.md) when selecting a new diagnostic's severity and resource.

Low importance does not make constructing arguments or payloads free. Use the established producer/logging helper's enabled/lazy path where appropriate, without suppressing information needed by a subscribed consumer.

## Find the correct event

| Information | Source/event to inspect |
|---|---|
| Final evaluated properties/items | Requested `ProjectEvaluationFinishedEventArgs` data |
| Structured initial property assignment | `PropertyInitialValueSetEventArgs`, when tracking is enabled |
| Property reassignment | Structured tracking or the existing message path, subject to filtering |
| Target execution/skipping | Target started/finished or skipped events for the actual execution path |
| Task input/output values | `TaskParameterEventArgs` and its input/output kind, subject to logging controls |
| Task completion | `TaskFinishedEventArgs`; it does not itself contain all output items/properties |
| Warnings/errors | Their structured event kind, code, message, and location |
| Imports | Import events plus the configured collection path |

Assert the event/field that represents the changed contract rather than searching only formatted text or assuming a task-finished event is a parameter dump.

## Project imports

[ProjectImportsCollector](../../../../src/Build/Logging/BinaryLogger/ProjectImportsCollector.cs) can collect project/import files and in-memory content. [BinaryLogger](../../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs) controls whether it is used:

| Mode | Result |
|---|---|
| `ProjectImports=None` | No collected import archive |
| `ProjectImports=Embed` | Embed the collected archive in the binlog |
| `ProjectImports=ZipFile` | Write a separate project-import archive |

`MSBUILDLOGIMPORTS` requests import logging; setting it alone is not equivalent to configuring a file collector or embedding every source file.

The collector normally queues disk-file collection on a background task and opens the file when that work runs. It deduplicates paths, checks availability, and can encounter I/O failures. This is not an atomic snapshot of exactly the bytes evaluation consumed. In-memory collection is a separate path.

For an import-collection change, distinguish disk and in-memory sources, already-processed files, collection mode, I/O behavior, and raw replay as relevant. Do not present a preprocessed viewer display as proof that every historical input byte is preserved.

## Sensitive content and evidence

Collected project/targets files and event values can contain sensitive information. `ProjectImports=None` avoids that archive, but does not scrub sensitive values from other events. Producing a binlog does not authorize uploading it or attaching it to a public issue.

Use a focused content assertion or existing reproduction when the task needs evidence. [Binary-Log.md](../../../../documentation/wiki/Binary-Log.md) describes general usage; [format and replay](format-and-replay.md) describes codec changes. State missing/filtered data rather than interpreting its absence as proof an operation did not happen.
