---
name: binary-log-considerations
description: 'Work on binary log infrastructure: when modifying BinaryLogger or BinaryLogReplayEventSource, adding new BuildEventArgs types, changing event serialization or deserialization, modifying ProjectImportsCollector, adjusting log message importance levels, or making changes that affect what gets captured in .binlog files. Also consult when verifying that a behavioral change is properly reflected in binary log output.'
argument-hint: 'Describe the binary log change or event serialization concern.'
---

# Binary Log Considerations

The binary log (`.binlog`) is MSBuild's primary diagnostic format and source of truth for build analysis. It captures the complete build event stream with full fidelity. Format changes have strict compatibility requirements.

For general binary log usage, see [Binary-Log.md](../../../documentation/wiki/Binary-Log.md).

## Core Principles

1. **The binlog captures everything.** All meaningful build events must be represented. If your change alters build behavior, it must be observable in the binlog.
2. **Format changes must be backward-compatible.** Older versions of MSBuild Structured Log Viewer and other tools must be able to read binlogs produced by newer MSBuild, at minimum gracefully degrading.
3. **Forward compatibility matters too.** Newer viewers should handle binlogs from older MSBuild without crashing, even if some events are unrecognized.

## Architecture Overview

```
Build Engine
  → BuildEventArgs (structured events)
    → BinaryLogger (serializes to .binlog)
      → ProjectImportsCollector (embeds project/targets files)

Replay:
  .binlog file
    → BinaryLogReplayEventSource (deserializes)
      → Any ILogger (console, structured log viewer, analyzers)
```

Key source files:
- `src/Build/Logging/BinaryLogger/BinaryLogger.cs` — the logger itself
- `src/Framework/BuildEventArgs.cs` — base class for all build events
- `src/Build/Logging/BinaryLogger/ProjectImportsCollector.cs` — captures imported files

## Adding New Build Event Types

When adding a new `BuildEventArgs` subclass:

1. **Define the new event class** inheriting from the appropriate base (`BuildMessageEventArgs`, `BuildWarningEventArgs`, etc.)
2. **Add serialization support** — implement `WriteToStream` and `CreateFromStream` methods
3. **Increment the binary log version** if the new event type changes the format
4. **Add a new record type constant** in the binary logger's record type enum
5. **Handle the unknown-type case** in the replay source — older readers must skip gracefully

### Serialization Compatibility Rules

- **Never remove fields** from existing event args serialization
- **New fields must be appended** to the end of the serialization stream
- **Use version checks** when reading — if the binlog version is older, use defaults for new fields
- **Nullable fields** should serialize a presence flag before the value

```csharp
// Pattern for backward-compatible field addition
if (logVersion >= newFieldVersion)
{
    writer.Write(newField);
}

// Reading with backward compatibility
if (logVersion >= newFieldVersion)
{
    newField = reader.ReadString();
}
else
{
    newField = defaultValue;
}
```

## Message Importance Levels

Importance controls what appears in console output, but **everything goes to binlog** regardless of importance.

| Level | Use For | Console Verbosity |
|-------|---------|------------------|
| `High` | Critical user-facing information | Minimal and above |
| `Normal` | Standard build progress | Normal and above |
| `Low` | Detailed diagnostic information | Detailed and above |
| `Diagnostic` | Internal debugging | Diagnostic only |

### Rules

- Default to `Normal` for user-relevant information
- Use `Low` for information useful when debugging but noisy in normal builds
- `High` is reserved for important warnings/status — use sparingly
- Never skip logging because "it's too verbose" — log at `Low` instead

## ProjectImportsCollector

The `ProjectImportsCollector` embeds all imported `.props`, `.targets`, and project files into the binlog. This enables the "preprocessed view" in log viewers.

### Considerations

- `MSBUILDLOGIMPORTS=1` (or the `/bl` switch) enables import collection
- Imported file content is captured at evaluation time — reflects the actual content used
- Large import chains increase binlog size — this is acceptable for diagnostic completeness
- Sensitive content in imported files will be embedded — document this for users

## Changes That Affect Binlog Content

When modifying MSBuild behavior, verify binlog impact:

| Change Type | Binlog Consideration |
|------------|---------------------|
| New property set during evaluation | Appears in `PropertyInitialValue` or `PropertyReassignment` events |
| New target added | Produces `TargetStarted`/`TargetFinished` events |
| Changed task behavior | Task output items/properties captured in `TaskFinished` |
| New warning/error | Captured as `BuildWarningEventArgs`/`BuildErrorEventArgs` |
| Modified import chain | Changes which files `ProjectImportsCollector` captures |

## Testing Binlog Changes

- **Round-trip test**: Write a binlog, replay it, verify all events are reconstructed
- **Version compatibility test**: Verify older replay sources handle new events gracefully
- **Content verification**: Assert specific events appear in the binlog for behavioral changes
- **Size regression**: Monitor binlog size for unexpectedly large increases
- Use `BinaryLogReplayEventSource` in tests to verify binlog content programmatically

## Checklist

- [ ] New event types have `WriteToStream`/`CreateFromStream` implementations
- [ ] Binary log format version incremented if format changed
- [ ] Backward compatibility: older readers skip/degrade gracefully
- [ ] Forward compatibility: newer readers handle old format
- [ ] Importance levels set correctly for new messages
- [ ] Behavioral changes produce observable binlog events
- [ ] Round-trip test passes (serialize → deserialize → verify)
