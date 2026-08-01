# Structured task logging

`TaskLoggingHelper` accepts C# interpolated strings as structured messages without adding a
dependency on `Microsoft.Extensions.Logging`.

```csharp
Log.LogMessage(
    MessageImportance.Low,
    $"Considered {candidate} but expected {expected}");
```

After recompilation, the interpolated-string-handler overload captures:

- the visible message using the current culture;
- an invariant named template (`Considered {candidate} but expected {expected}`);
- ordered, uniquely named values formatted using the invariant culture.

The visible message is represented by a positional composite template and arguments on the
existing lazy build-event base class. The complete string is not created unless a logger or other
consumer requests `BuildEventArgs.Message`. Materializing `Message` does not change or discard the
independent named template and values.

The handler constructor calls `LogsMessagesOfImportance` and returns `shouldAppend: false` when
the message is disabled. In that case interpolation expressions are not evaluated and no message,
template, collection, or boxed value is allocated.

## Names

Hole names default to the source expression captured by `CallerArgumentExpression`. Characters
that conflict with named-template syntax are replaced with `_`. Repeated names are made unique in
occurrence order by appending `_2`, `_3`, and so on.

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
and an ordered `IReadOnlyList<KeyValuePair<string, object>>`. Names must match the template in
occurrence order. This keeps localized display semantics separate from the persisted invariant
template.

## Compatibility and transport

Existing string, preformatted-string, and positional composite-format calls keep their existing
overloads. Interpolated expressions select the handler overload only when source is recompiled.
Code compiled against the new overloads requires the corresponding newer
`Microsoft.Build.Utilities.Core` assembly at runtime.

Structured messages use the existing extended message, warning, and error event kinds. Warning
suppression, warning-as-error routing, codes, help links, and source locations therefore use the
same engine paths as ordinary diagnostics.

The invariant template and values ride the existing `ExtendedData` and `ExtendedMetadata` transport,
so no binary-log or node-packet format change is required. Metadata keys contain a fixed-width
occurrence index plus the unique name, preserving order independently of dictionary enumeration.
Metadata values carry a one-character tag so null remains distinct from an empty string. The binary
logger's existing string and name/value-list tables deduplicate every component. New readers
reconstruct `IStructuredBuildEventArgs`; older readers retain the visible diagnostic and expose the
extended fields without needing to understand the convention. Such readers may display raw encoded
metadata (for example, `00000000:Candidate=1a.dll`).

`MSBuild.StructuredLogging` is reserved as the `ExtendedType` identifying this convention. Custom
extended events must not use that identifier.

Selecting the familiar `LogMessage`, `LogWarning`, and `LogError` handler overloads after
recompilation is intentional: existing interpolated call sites become structured without source
edits. This changes their event subtype and binlog metadata, while preserving visible text and
diagnostic behavior. Literal strings, preformatted string variables, and positional composite
format calls retain their existing overloads.

This is a new opt-in API surface and does not change existing build behavior, so it is not gated by
a Change Wave.
