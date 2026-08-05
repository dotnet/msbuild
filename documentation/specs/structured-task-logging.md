# Structured task logging

`TaskLoggingHelper` can capture C# interpolated strings as structured messages.
This feature does not add a dependency on `Microsoft.Extensions.Logging`.

```csharp
Log.LogMessage(
    MessageImportance.Low,
    $"Considered {candidate} but expected {expected}");
```

After recompilation, the interpolated string handler captures:

- An invariant named template (`Considered {candidate} but expected {expected}`).
- Ordered values with unique names.

The handler formats each value one time with the task's current culture.
This operation makes the displayed message identical before and after transport.

The event uses the named template as its display format.
Structured consumers combine this template with the ordered values.
The `Message` property renders the display text only when a consumer reads the property.
This operation does not remove the named template or its values.

The structured path does not store a positional `{0}` template or a second argument array.
The Change Wave fallback creates that data only when it emits an ordinary event.

The handler constructor calls `LogsMessagesOfImportance`.
For a disabled message, the constructor returns `shouldAppend: false`.
The compiler then does not evaluate the interpolation expressions.
The handler does not allocate a message, template, collection, or boxed value.

Structured logging stores more state than an ordinary positional message.
In a 500-event test, structured events used 20,561 bytes.
Equivalent lazy composite events used 20,022 bytes.
Thus, the dedicated representation was 2.7% larger in this test.

The main benefits are stable names and zero allocation for filtered interpolation.
Do not use this feature as a general binary-log size optimization.

## Names

The compiler supplies each default hole name through `CallerArgumentExpression`.
The handler keeps simple identifiers and dotted paths.
The handler uses `ValueN` for other expressions.
For duplicate names, the handler adds `_2`, `_3`, and subsequent suffixes in occurrence order.

Use `TaskLoggingHelper.Named` when an expression is not a stable name:

```csharp
Log.LogMessage($"Project {Log.Named("ProjectPath", project.FullPath)}");
```

`Named` does not change the standard interpolation format specifier.

## Dynamic callers and localization

Languages without interpolated string handlers can use ordered values:

```csharp
Log.LogStructuredMessage(
    MessageImportance.Low,
    "Considered {Candidate} but expected {Expected}",
    candidate,
    expected);
```

Use `LogStructuredWarning` and `LogStructuredError` for structured diagnostics.
These APIs throw `FormatException` for a malformed template or an incorrect value count.
This behavior matches the existing composite-format APIs.

For localized text, use the overload that accepts an invariant template and a localized message.
The overload also accepts an ordered `IReadOnlyList<KeyValuePair<string, object?>>`.
Each value name must match its template hole.
This design keeps the localized text separate from the invariant template.

## Compatibility and transport

Existing string, preformatted-string, and positional composite-format calls keep their current overloads.
An interpolated expression selects the handler overload only after source recompilation.
Code that uses the new overload requires the corresponding new `Microsoft.Build.Utilities.Core` assembly at runtime.

Structured messages use dedicated message, warning, and error event types.
They use the ordinary engine paths for warning suppression and warning-as-error routing.
They also preserve codes, help links, and source locations.

Node packets serialize the invariant template and ordered name-value pairs directly.
Binary-log format version 28 adds three length-prefixed record kinds.
Each record stores names and values as separate string-table references.
This representation preserves order and distinguishes a null value from an empty value.
It does not use generic dictionaries, synthetic keys, type tags, or copied value strings.

Forward-compatible readers that predate version 28 skip the unknown structured records.
These readers cannot display the skipped events.
The dedicated records do not contain an encoded legacy message record.

Set `MSBUILDDISABLEFEATURESFROMVERSION=18.11` when a consumer requires an older reader.
This setting restores ordinary message, warning, and error event types.

After recompilation, existing interpolated calls select the familiar handler overloads without source changes.
This selection changes the event subtype and binary-log record.
It does not change the displayed text or diagnostic behavior.
Literal strings, preformatted variables, and positional composite formats keep their existing overloads.

Change Wave 18.11 controls the dedicated event types.
The Change Wave opt-out restores `BuildMessageEventArgs`, `BuildWarningEventArgs`, and `BuildErrorEventArgs`.
