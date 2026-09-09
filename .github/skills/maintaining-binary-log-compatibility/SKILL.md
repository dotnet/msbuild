---
name: maintaining-binary-log-compatibility
description: "Change MSBuild structured build events, binary-log codecs, replay compatibility, or import collection. Not routine binlog analysis, ordinary diagnostic severity selection, or a requirement to add logging for every behavior change."
argument-hint: "Identify the event/codec/content change and required reader or host compatibility."
---

# Maintain binary-log contracts

**Use for:** a changed serialized event, record/field format, replay path, event-content contract, or embedded-import behavior.

**Do not use for:** merely opening a binlog or adding an ordinary resource-based diagnostic. Use [diagnostic authoring](../authoring-errors-and-warnings/SKILL.md) when no format/content boundary changes.

## Source preflight

Read the affected producer and [logging instructions](../../instructions/logging.instructions.md). Inspect [BinaryLogger](../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs), [BuildEventArgsWriter](../../../src/Build/Logging/BinaryLogger/BuildEventArgsWriter.cs), and [BuildEventArgsReader](../../../src/Build/Logging/BinaryLogger/BuildEventArgsReader.cs).

For a new/changed event class, also inspect its separate [node transport](../../../src/Shared/LogMessagePacketBase.cs) and [Framework instructions](../../instructions/framework.instructions.md). Do not infer binlog support from event `WriteToStream` methods.

## Workflow

1. Classify the change: emitted content, node transport, binlog codec, reader behavior, or more than one.
2. Follow [format and replay](references/format-and-replay.md) for the required registrations, codecs, version decision, and supported compatibility boundary.
3. Follow [event content](references/event-content.md) for importance, output events, filtering, import modes, and privacy. A binlog is not a guaranteed full-fidelity snapshot.
4. Preserve null/empty distinctions, existing field contracts, and version-specific reading. Coordinate the relevant Structured Log implementation when its codec must change.
5. Select round-trip, historical/future-version, transport, or content evidence for the changed boundary. Do not promise every old reader can gracefully read every new format.

## Evidence and stop condition

Stop when the requested contract is supported by the necessary source/round-trip/content evidence and remaining reader/host limits are stated. An investigation does not authorize producing or uploading logs, changing unrelated diagnostics, or running a full build merely because logging code exists.
