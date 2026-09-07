# Binary-log format and replay

## Two serialization channels

| Channel | Owning implementation |
|---|---|
| Build-event transport between MSBuild processes | Event `WriteToStream` / `CreateFromStream` and [LogMessagePacketBase](../../../../src/Shared/LogMessagePacketBase.cs) |
| Binlog event encoding | [BuildEventArgsWriter](../../../../src/Build/Logging/BinaryLogger/BuildEventArgsWriter.cs), [BinaryLogRecordKind](../../../../src/Build/Logging/BinaryLogger/BinaryLogRecordKind.cs), and [BuildEventArgsReader](../../../../src/Build/Logging/BinaryLogger/BuildEventArgsReader.cs) |
| Replay routing | [BinaryLogReplayEventSource](../../../../src/Build/Logging/BinaryLogger/BinaryLogReplayEventSource.cs) and [EventArgsDispatcher](../../../../src/Build/Logging/BinaryLogger/EventArgsDispatcher.cs) |

An event class implementing its transport methods is not enough to preserve new fields in a binlog. `BuildEventArgsWriter.WriteCore` and its typed dispatch determine what is encoded. Its fallback can convert unrecognized events to messages, preserving only the supported message/extended data.

For external task/logger extension data, inspect the supported `Extended*EventArgs` contract rather than assuming an arbitrary subclass's fields will round-trip. Internal product event additions need the relevant explicit registrations.

## Adding or changing an event

1. Identify the event's base contract and every producer/consumer that needs the new information.
2. Check node transport separately, including known event construction/dispatch and affected host versions.
3. Add or update binlog writer dispatch and the typed encoder.
4. Add a record kind when the wire contract requires a distinct kind; preserve existing enum values.
5. Add reader dispatch/decoding and relevant replay routing.
6. Decide format/minimum-reader version implications and coordinate the matching Structured Log codec.

[BinaryLogger.cs](../../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs) keeps the format history and explicitly calls out synchronization with the Structured Log writer. A repository-only encoder change is not proof that downstream tooling consumes it.

## Field encoding

Preserve existing field ordering and meaning. Append fields where that preserves the supported reader contract, and supply appropriate defaults when reading older formats. A writer generally emits the current format; readers retain historical branches. Do not copy pseudocode implying the writer has an arbitrary `logVersion` variable.

Use the existing field helpers:

- Strings/name-value data can be deduplicated and encoded by IDs, rather than written inline.
- `BuildEventArgsWriter.HashString` distinguishes null (ID 0) from the empty string (ID 1).
- Existing common-field flags encode the presence of various fields.
- A nullable field does not universally require a newly inserted Boolean; follow the particular codec.
- Preserve timestamp, event context, location, and other structured metadata as required, not just formatted `Message`.

The writer buffers records and emits dependent string/name-value records in the required order. Changes to auxiliary records or deduplication can affect more than one event type.

## Version decisions

Read the constants and history in [BinaryLogger.cs](../../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs), rather than copying current version numbers into a procedure.

| Constant | Purpose |
|---|---|
| `FileFormatVersion` | Current emitted representation; update when the format changes |
| `MinimumReaderVersion` | Earliest reader capable of the format's fundamental representation |
| `ForwardCompatibilityMinimalVersion` | Historical boundary at which framed records support forward-compatible reading; not a release counter |

A new event or appended field can often leave the minimum reader unchanged because compatible readers can skip unknown data. A change to framing/auxiliary representation may require raising it. Derive the decision from what an older reader can actually parse.

Use explicit directions:

- **Backward reading:** a newer reader reads older logs using its historical decode/default branches.
- **Forward-compatible reading:** an older reader encounters a newer log, when the format/minimum-reader contract supports it and the reader opts into skipping unknown content.

`BinaryLogReplayEventSource.AllowForwardCompatibility` is opt-in. `OpenBuildEventsReader` rejects unsupported combinations, including a minimum reader newer than the implementation. Unknown-content skipping also depends on framed records and the version relationship.

Direct `BuildEventArgsReader` skipping requires the recoverable-error notification contract; inspect `CheckErrorsSubscribed` rather than silently discarding unknown data. Partial recovery is not proof of complete event fidelity.

Raw replay/transcoding and structured replay differ. [BinaryLogger](../../../../src/Build/Logging/BinaryLogger/BinaryLogger.cs) preserves source version information when copying raw records; stamping a new version over unchanged old bytes would misdescribe the payload.

## Select evidence for the changed contract

[BinaryLogger_Tests](../../../../src/Build.UnitTests/BinaryLogger_Tests.cs) contains round-trip/equality scenarios, raw versus structured replay, and forward-compatibility acceptance/rejection cases.

Choose the relevant assertions:

| Change | Evidence |
|---|---|
| New field/event | Required structured data survives the relevant serialization/replay paths |
| Historical reader branch | Representative older-format data yields the intended defaults/fields |
| Forward-compatible recovery | Accepted/rejected version combinations, recoverable unknown content, and visible limits |
| Node transport | The event reaches the consumer with needed data in the affected process/host path |
| Import/embedded content | Correct collection mode and embedded/external content, including applicable failures |
| Large/repeated payload | Meaningful size/allocation regression evidence |

Do not require every row for an ordinary message-text change. Do not infer compatibility with every third-party viewer or host from one local round trip.
