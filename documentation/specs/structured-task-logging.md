# Structured task logging

`TaskLoggingHelper` accepts C# interpolated strings as structured messages without adding a
dependency on `Microsoft.Extensions.Logging`.

```csharp
Log.LogMessage(
    MessageImportance.Low,
    $"Considered {candidate} but expected {expected}");
```

After recompilation, the interpolated-string-handler overload captures:

- an invariant named template (`Considered {candidate} but expected {expected}`);
- ordered, uniquely named values formatted once using the task's current culture.

The event's display template is the named structured template itself. Structured event consumers
render it against the ordered values; the event's `Message` property provides a lazy compatibility
renderer for existing loggers. No positional `{0}`/`{1}` template or duplicate display-argument
array is stored when structured events are enabled. Materializing `Message` does not change or
discard the named template and values.

The handler constructor calls `LogsMessagesOfImportance` and returns `shouldAppend: false` when
the message is disabled. In that case interpolation expressions are not evaluated and no message,
template, collection, or boxed value is allocated.

When enabled, structured logging necessarily carries more state than an ordinary positional
composite message. It should not be described as a general binlog-size optimization: a representative
500-event comparison produced 20,561 serialized bytes for structured events and 20,022 bytes for
existing lazy composite events. The dedicated representation is therefore 2.7% larger in this
workload; its direct benefits are stable names and a zero-allocation filtered interpolation path.

## Names

Hole names default to the source expression captured by `CallerArgumentExpression`. Simple
identifiers and dotted paths are retained; expressions that would make an unstable schema receive
`ValueN`. Repeated names are made unique in occurrence order by appending `_2`, `_3`, and so on.

Use `TaskLoggingHelper.Named` when an expression is not a stable name:

```csharp
Log.LogMessage($"Project {Log.Named("ProjectPath", project.FullPath)}");
```

This does not reserve or reinterpret the normal interpolation format specifier.

## Dynamic callers and localization

Languages without interpolated string handlers can use ordered values:

```csharp
Log.LogStructuredMessage(
    MessageImportance.Low,
    "Considered {Candidate} but expected {Expected}",
    candidate,
    expected);
```

`LogStructuredWarning` and `LogStructuredError` provide the corresponding diagnostic APIs.
Malformed templates or a value-count mismatch throw `FormatException`, matching the existing
composite-format APIs.

Callers with localized display text can supply the invariant template, already-localized message,
and an ordered `IReadOnlyList<KeyValuePair<string, object?>>`. Names must match the template in
occurrence order. This keeps localized display semantics separate from the persisted invariant
template.

## Compatibility and transport

Existing string, preformatted-string, and positional composite-format calls keep their existing
overloads. Interpolated expressions select the handler overload only when source is recompiled.
Code compiled against the new overloads requires the corresponding newer
`Microsoft.Build.Utilities.Core` assembly at runtime.

Structured messages use dedicated message, warning, and error event types. Warning
suppression, warning-as-error routing, codes, help links, and source locations therefore use the
same engine paths as ordinary diagnostics.

Node packets serialize the invariant template and ordered name/value pairs directly. Binary-log format
version 28 adds three length-prefixed record kinds and stores each name and value as an independent
string-table reference. The representation therefore preserves order, null, and empty values without
generic dictionaries, synthetic keys, tags, or copied value strings.

Forward-compatible readers that predate version 28 skip the unknown structured records cleanly. They
cannot display those events because the record type is intentionally parallel to, rather than encoded
as, a legacy message. Setting `MSBUILDDISABLEFEATURESFROMVERSION=18.11` temporarily restores ordinary
message, warning, and error event shapes for consumers that require an older reader.

Selecting the familiar `LogMessage`, `LogWarning`, and `LogError` handler overloads after
recompilation is intentional: existing interpolated call sites become structured without source
edits. This changes their event subtype and binlog metadata, while preserving visible text and
diagnostic behavior. Literal strings, preformatted string variables, and positional composite
format calls retain their existing overloads.

Recompilation changes interpolated call sites to extended structured events. This emitted event
shape is gated by Change Wave 18.11 so affected logger or transport consumers can temporarily
restore ordinary `BuildMessageEventArgs`, `BuildWarningEventArgs`, and `BuildErrorEventArgs`.
