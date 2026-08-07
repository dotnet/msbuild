# Structured task logging

- **Status:** Proposed
- **Implementation prototype:** [PR #14606](https://github.com/dotnet/msbuild/pull/14606)
- **Target Change Wave:** 18.11

## Summary

Materialized task messages combine fixed text and values into one string.
These strings often vary per event, which limits binary-log string deduplication.

The primary goal is to reduce binary-log size for repeated message shapes.
Structured events carry a reusable `OriginalFormat` and separate values instead of a materialized display string.
This shape lets the binary logger deduplicate each component.

The same structure also preserves the meaning of interpolated values.
This proposal does not add a dependency on `Microsoft.Extensions.Logging`.

C# task authors can use normal interpolated strings.
The compiler selects an interpolated string handler after the task recompiles.
The handler captures a named template and an ordered list of formatted values.

Existing loggers continue to read `BuildEventArgs.Message`.
Structured loggers can also read `IStructuredBuildEventArgs`.

This document describes the proposed design, not the current implementation contract.
The [open questions](#open-questions) require team review.

## Problem

`TaskLoggingHelper` supports lazy composite formats such as `"Resolved {0}"`.
These formats preserve values until a logger reads the message.
However, numeric holes do not identify the meaning of each value.

C# interpolation gives readable task code.
However, the current string overload evaluates every interpolation hole before MSBuild can filter the message.
Interpolation also removes source names that loggers need for queries and grouping.
It also sends materialized display text that often contains a different string for each event.
That representation reduces string-table reuse in binary logs.

MSBuild needs a logging path with these properties:

- It preserves stable names for structured analysis.
- It keeps existing display text and diagnostic behavior.
- It avoids hole evaluation when message importance disables the message.
- It works across nodes and binary-log replay.
- It does not require all existing loggers to understand structured data.

## Goals

- Add a natural C# API for structured task messages, warnings, and errors.
- Add an explicit API for dynamic callers and languages without interpolated string handlers.
- Reduce binary-log size when many events share a template but contain different values.
- Preserve codes, help data, source locations, message importance, and warning policy.
- Preserve one invariant template and ordered values through node and binary-log transport.
- Keep `Message` compatible with existing loggers.
- Avoid allocations and hole evaluation for importance-filtered messages.
- Keep existing compiled tasks binary compatible.

## Non-goals

- Replace `Microsoft.Extensions.Logging` or implement its complete API.
- Add `EventId`, logging scopes, or dependency injection.
- Change the existing composite-format overloads.
- Change warning suppression or warning-as-error policy.
- Preserve arbitrary object types across a process boundary.
- Redact secrets automatically.
- Guarantee smaller binary logs.
- Convert existing task call sites automatically.

## Terminology

**Display message**
: The localized text returned by `BuildEventArgs.Message`.

**Original format**
: The invariant named template that identifies the event schema.
This term follows the `Microsoft.Extensions.Logging` `{OriginalFormat}` convention.

**Structured value**
: A name and a nullable formatted string.

**Capture**
: The operation that creates the original format and structured values.

**Materialization**
: The operation that creates the display message from captured state.

**Classic event**
: An existing `BuildMessageEventArgs`, `BuildWarningEventArgs`, or `BuildErrorEventArgs` instance without structured state.

## User scenarios

### C# task author

A task author writes an interpolated message:

```csharp
Log.LogMessage(
    MessageImportance.Low,
    $"Resolved {candidate} from {searchPath}");
```

After recompilation, MSBuild captures this state:

```text
OriginalFormat: "Resolved {candidate} from {searchPath}"
StructuredValues: [("candidate", "candidate.dll"), ("searchPath", "/packages/reference")]
Message: "Resolved candidate.dll from /packages/reference"
```

### Logger author

A logger continues to read `BuildEventArgs.Message`.
The logger can also test for `IStructuredBuildEventArgs`.
The logger can group events by `OriginalFormat` without materializing `Message`.

### Dynamic or non-C# caller

A caller supplies a named template and ordered named values:

```csharp
IReadOnlyList<KeyValuePair<string, object?>> values =
[
    new("Candidate", candidate),
    new("SearchPath", searchPath),
];

Log.LogStructuredMessage(
    MessageImportance.Low,
    "Resolved {Candidate} from {SearchPath}",
    values);
```

### Localized task

A resource-based task supplies localized display text and a separate invariant template:

```csharp
Log.LogStructuredMessage(
    MessageImportance.Low,
    "Copied {Source} to {Destination}",
    localizedMessage,
    new("Source", source),
    new("Destination", destination));
```

## Proposed task-author API

### Interpolated string API

`TaskLoggingHelper` adds these public nested types:

```csharp
public readonly struct NamedStructuredLogValue<T>
{
    // This type has no public members. TaskLoggingHelper.Named<T> creates its values.
}

[InterpolatedStringHandler]
public ref struct StructuredLogInterpolatedStringHandler
{
    public StructuredLogInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        out bool shouldAppend);

    public StructuredLogInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        TaskLoggingHelper logger,
        out bool shouldAppend);

    public StructuredLogInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        TaskLoggingHelper logger,
        MessageImportance importance,
        out bool shouldAppend);

    public void AppendLiteral(string value);

    public void AppendFormatted<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? expression = null);

    public void AppendFormatted<T>(
        T value,
        string? format,
        [CallerArgumentExpression(nameof(value))] string? expression = null);

    public void AppendFormatted<T>(
        T value,
        int alignment,
        [CallerArgumentExpression(nameof(value))] string? expression = null);

    public void AppendFormatted<T>(
        T value,
        int alignment,
        string? format,
        [CallerArgumentExpression(nameof(value))] string? expression = null);

    public void AppendFormatted<T>(NamedStructuredLogValue<T> value);

    public void AppendFormatted<T>(
        NamedStructuredLogValue<T> value,
        string? format);

    public void AppendFormatted<T>(
        NamedStructuredLogValue<T> value,
        int alignment);

    public void AppendFormatted<T>(
        NamedStructuredLogValue<T> value,
        int alignment,
        string? format);
}
```

`TaskLoggingHelper.Named<T>(string name, T value)` creates a value with an explicit structured name.

The existing type gains these handler overloads.
The API surface omits unchanged members:

```csharp
public partial class TaskLoggingHelper
{
    public void LogMessage(
        [InterpolatedStringHandlerArgument("")]
        ref StructuredLogInterpolatedStringHandler message);

    public void LogMessage(
        MessageImportance importance,
        [InterpolatedStringHandlerArgument("", nameof(importance))]
        ref StructuredLogInterpolatedStringHandler message);

    public void LogMessage(
        string? subcategory,
        string? code,
        string? helpKeyword,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        MessageImportance importance,
        [InterpolatedStringHandlerArgument("", nameof(importance))]
        ref StructuredLogInterpolatedStringHandler message);

    public void LogWarning(
        string warningCode,
        ref StructuredLogInterpolatedStringHandler message);

    public void LogWarning(
        string? subcategory,
        string warningCode,
        string? helpKeyword,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ref StructuredLogInterpolatedStringHandler message);

    public void LogWarning(
        string? subcategory,
        string warningCode,
        string? helpKeyword,
        string? helpLink,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ref StructuredLogInterpolatedStringHandler message);

    public void LogError(
        string errorCode,
        ref StructuredLogInterpolatedStringHandler message);

    public void LogError(
        string? subcategory,
        string errorCode,
        string? helpKeyword,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ref StructuredLogInterpolatedStringHandler message);

    public void LogError(
        string? subcategory,
        string errorCode,
        string? helpKeyword,
        string? helpLink,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ref StructuredLogInterpolatedStringHandler message);
}
```

The overloads use `ref StructuredLogInterpolatedStringHandler`.
Message overloads pass the helper and importance to the handler constructor.
Warning and error overloads use a constructor that always captures.

### Overload resolution and runtime requirements

An interpolated string expression selects the handler overload after source recompilation.
A literal string or a string variable continues to select an existing string overload.
A positional composite call continues to select its existing overload.

```csharp
// Selects the new handler overload after recompilation.
Log.LogMessage($"Resolved {candidate}");

// Selects the existing string overload.
Log.LogMessage("Resolution completed");

string message = GetMessage();
Log.LogMessage(message);

// Selects the existing positional composite overload.
Log.LogMessage("Resolved {0}", candidate);
```

An already-compiled task contains references to the old methods.
The runtime behavior of that task does not change.

A recompiled task contains references to the new handler methods.
That task requires a matching `Microsoft.Build.Utilities.Core` assembly at runtime.

Dynamic dispatch must not depend on handler overload selection.
Dynamic callers must use the explicit structured methods.

### Explicit structured API

The proposal adds these overload families:

```csharp
void LogStructuredMessage(
    string messageTemplate,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);

void LogStructuredMessage(
    MessageImportance importance,
    string messageTemplate,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);

void LogStructuredWarning(
    string warningCode,
    string messageTemplate,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);

void LogStructuredError(
    string errorCode,
    string messageTemplate,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);
```

These signatures show the explicit-parameter option.
The placement of diagnostic metadata remains open for review.

Explicit parameters make codes, locations, and help data discoverable.
They also preserve the established `BuildWarningEventArgs` and `BuildErrorEventArgs` properties.

Reserved entries in `values` would reduce overload count and permit extensible metadata.
However, reserved entries are less discoverable and can conflict with template value names.
That design would need a reserved-name contract and strict validation.

The `params ReadOnlySpan<T>` overloads give C# callers an allocation-free argument container.
The caller supplies each structured name explicitly.

Dynamic and non-C# callers use equivalent `IReadOnlyList<KeyValuePair<string, object?>>` overloads.
These overloads avoid a stack-only parameter that dynamic dispatch cannot box.

```csharp
void LogStructuredMessage(
    string messageTemplate,
    IReadOnlyList<KeyValuePair<string, object?>> values);

void LogStructuredMessage(
    MessageImportance importance,
    string messageTemplate,
    IReadOnlyList<KeyValuePair<string, object?>> values);

void LogStructuredWarning(
    string warningCode,
    string messageTemplate,
    IReadOnlyList<KeyValuePair<string, object?>> values);

void LogStructuredError(
    string errorCode,
    string messageTemplate,
    IReadOnlyList<KeyValuePair<string, object?>> values);
```

Localized overloads accept a code for diagnostics, invariant template, localized display text, and ordered named values:

```csharp
void LogStructuredMessage(
    MessageImportance importance,
    string originalFormat,
    string localizedMessage,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);

void LogStructuredWarning(
    string warningCode,
    string originalFormat,
    string localizedMessage,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);

void LogStructuredError(
    string errorCode,
    string originalFormat,
    string localizedMessage,
    params ReadOnlySpan<KeyValuePair<string, object?>> values);
```

The localized interop overloads use the same parameters with an `IReadOnlyList`:

```csharp
void LogStructuredMessage(
    MessageImportance importance,
    string originalFormat,
    string localizedMessage,
    IReadOnlyList<KeyValuePair<string, object?>> values);

void LogStructuredWarning(
    string warningCode,
    string originalFormat,
    string localizedMessage,
    IReadOnlyList<KeyValuePair<string, object?>> values);

void LogStructuredError(
    string errorCode,
    string originalFormat,
    string localizedMessage,
    IReadOnlyList<KeyValuePair<string, object?>> values);
```

Every structured warning and error overload requires a non-empty code.
This requirement keeps suppression and warning-as-error policy manageable.

The template parser requires one value for each hole.
Malformed braces, unnamed holes, and count mismatches cause `FormatException`.
Named-list mismatches cause `ArgumentException`.

Source locations, help keywords, and help links for dynamic callers remain an open question.

## Name rules

The compiler supplies each default name through `CallerArgumentExpression`.
The handler keeps simple identifiers and dotted paths.
Valid inferred names start with a letter or underscore.
Remaining characters can be letters, digits, underscores, or periods.

The prototype replaces another expression with `Value0`, `Value1`, and subsequent fallback names.
Examples include method calls, indexers, operators, and literals.
The starting number remains open for review.

Names use ordinal, case-sensitive comparison.
The prototype adds `_2`, `_3`, and subsequent suffixes to repeated names.
It also updates the original format with those suffixes.
The required duplicate-name behavior remains open for review.

`Named` lets a task author request a stable name:

```csharp
Log.LogMessage(
    $"Project {Log.Named("ProjectPath", project.FullPath)}");
```

The exact validation rule for explicit names remains open.
The implementation must not silently change an explicit stable name.

## Formatting and localization

Capture formats each non-null value one time.
The prototype passes its format specifier and `CultureInfo.CurrentCulture` to `IFormattable`.
Otherwise, capture uses `ToString()`.

The capture culture remains open for review.
Current culture preserves existing display text.
Invariant culture gives structured values a stable representation across build environments.
Invariant capture can change display text unless the event stores a separate display representation.

Structured metadata stores the formatted string, not the raw object.
This rule makes in-process output and replay output identical.
It also prevents arbitrary object serialization across nodes.

A null value remains null in structured metadata.
The display message renders that value as an empty string.
An empty string remains different from null in structured metadata.

Alignment remains part of the original format.
Materialization applies alignment to the captured formatted string.
Positive alignment pads on the left.
Negative alignment pads on the right.

Literal braces use the normal interpolation and composite-format escape rules.
Format text does not become part of the structured name.

The localized overload does not render the display message from the invariant template.
It returns the supplied localized text.
It still captures formatted structured values for analysis.

The localized value names must match the invariant template holes in order.
This rule prevents translated text from changing the structured schema.

## Structured metadata contract

The public logger contract is:

```csharp
public interface IStructuredBuildEventArgs
{
    string? OriginalFormat { get; }

    IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues { get; }
}
```

`OriginalFormat` identifies the event schema.
Each template hole corresponds to the value at the same list position.

`StructuredValues` preserves occurrence order.
The list can represent repeated names without losing their positions.
Consumers must not assume that names are unique until the duplicate-name policy is final.

The contract exposes strings because capture completes formatting before transport.
The contract does not expose raw objects, type tags, or formatting providers.

Reading `Message` does not remove or change structured metadata.
Reading structured metadata does not materialize `Message`.

## Message materialization

For a normal structured event, the raw message field contains the named template.
The first `Message` read renders the display text and caches it.
Later reads return the cached text.

For a localized structured event, the raw message field contains localized text.
The event stores the invariant template as an override.
`Message` returns the localized text without template rendering.

Materialization uses the captured strings.
It does not call the original values or formatters again.

## Disabled logging semantics

### Messages

The message handler calls `LogsMessagesOfImportance` in its constructor.
For a disabled importance, the constructor returns `shouldAppend: false`.

The compiler then skips interpolation literals and holes.
The path does not create a template, value list, argument array, or message.
Expressions with side effects do not run.

The explicit `LogStructuredMessage` overload also checks importance before template parsing.
Callers still evaluate normal method arguments before that method starts.

### Warnings and errors

Warning and error handlers always capture.
MSBuild must receive warnings before it can apply `NoWarn`, warnings-as-messages, or warnings-as-errors policy.
MSBuild must receive errors because errors determine build success.

This difference is intentional.
The task helper must not apply engine warning policy before event creation.

When the engine converts a structured warning, it preserves the original format and structured values.
This rule applies to warning-to-message and warning-to-error conversion.

## Event model

The in-process implementation uses three internal event classes:

- A structured subclass of `BuildMessageEventArgs`.
- A structured subclass of `BuildWarningEventArgs`.
- A structured subclass of `BuildErrorEventArgs`.

Each class implements `IStructuredBuildEventArgs`.
The classes remain internal because logger code depends on the interface.

Existing loggers receive a familiar base event type.
They can read codes, locations, help data, importance, and `Message` without changes.

The design does not add structured state to `IExtendedBuildEventArgs`.
Extended events have a general metadata contract with different transport and compatibility costs.

## Node transport

Node packets add dedicated event identifiers for structured messages, warnings, and errors.
The packet writes the normal base-event fields and then writes structured state.

Structured state contains:

1. An optional original-format override.
2. The ordered value count.
3. Each value name.
4. Each nullable formatted value.

The override is absent when the raw message already contains the original format.
Localized events use the override because their raw message contains localized text.

Node transport preserves order and the difference between null and empty strings.
It does not send JSON, dictionaries, raw objects, or type tags.

Every receiving path must dispatch each structured event to the matching `BuildEngine.Log*Event` method.
This requirement includes worker nodes and out-of-process task hosts.
Dropping an error event can incorrectly let a build succeed.

### Prototype TaskHost gap

PR #14606 assigns node event identifiers to the three structured event types.
However, its `TaskHostTask` receive switch does not handle those identifiers.
The same-version out-of-process TaskHost path can silently discard structured events.

This gap is an implementation blocker, not a proposed behavior.
The implementation must add receive-side routing and end-to-end tests before merge.

## Binary log format

The prototype increments the binary-log format from version 27 to version 28.
Version 28 adds three length-prefixed record kinds:

- `StructuredMessage`
- `StructuredWarning`
- `StructuredError`

The minimum reader version remains 18.
Version 18 introduced record lengths that permit a forward-compatible reader to skip unknown records.

Each record writes the normal base-event fields first.
It then writes the optional original-format override and ordered values.

The binary logger writes names and values through the existing string table.
Repeated templates, names, and values can use existing string deduplication.

The record does not contain an encoded classic event.
Therefore, an old reader that skips the record also skips the complete event.

A version 28 reader reads older binary logs without a structured-data requirement.
Record lengths let a version 18 through 27 reader skip an unknown record without losing stream alignment.
Actual behavior depends on the reader and its forward-compatibility configuration.

The current replay API defaults and the Binary Log documentation do not agree.
This proposal does not define old-reader behavior until the team resolves that difference.

See [Binary Log](../../wiki/Binary-Log.md) for the existing format and replay model.

## Compatibility and Change Wave

The visible text and diagnostic policy remain unchanged when the structured path is active.
However, recompilation changes the event subtype and binary-log record.
Those output changes can affect loggers and tools.

Change Wave 18.11 gates dedicated structured events.
The proposed opt-out is:

```text
MSBUILDDISABLEFEATURESFROMVERSION=18.11
```

The opt-out restores classic message, warning, and error events.
The handler creates a positional composite format and arguments only for this fallback.

The Change Wave is necessary because the feature activates through overload resolution after recompilation.
The task author does not set a separate feature property.

Compatibility expectations are:

| Consumer | Expected behavior |
|---|---|
| Existing compiled task | Calls existing APIs and produces existing events |
| Recompiled task on new runtime | Selects handler overloads and can produce structured events |
| Recompiled task on old Utilities runtime | Cannot bind the new method references |
| Existing logger on new runtime | Receives base event types and reads unchanged display text |
| New logger | Can also read `IStructuredBuildEventArgs` |
| New reader with an old binary log | Replays classic events normally |
| Forward-compatible old reader | Can omit unknown structured records when its consumer enables skipping |
| Other old readers | Behavior needs consumer-specific verification |
| Change Wave opt-out | Produces classic records for full old-reader fidelity |

Mixed-version node behavior needs a specific compatibility decision.
The current node protocol requires both endpoints to recognize the new event identifiers.

The prototype also adds `[Serializable]` to three extended event types.
That change is not part of this design.
The implementation PR must remove or justify the incidental change.

See [Change Waves](../../wiki/ChangeWaves.md) for the general opt-out model.

## Performance and allocations

The prototype stores more state than classic composite logging.
It also performs name capture and value formatting before a consumer reads `Message`.

The disabled message path removes work.
It skips hole evaluation and all per-call allocations.

The following measurements are implementation evidence.
They are not API guarantees or release thresholds.

**Source and environment:** local sibling worktree, BenchmarkDotNet MediumRun (2 launches, 10 warmup iterations, and 15 measured iterations), .NET 11 arm64, `MemoryDiagnoser`, August 2026.

| Scenario | Classic composite | Structured interpolation | Observed change |
|---|---:|---:|---:|
| Enabled capture, two strings | 45.55 ns, 184 B | 108.25 ns, 328 B | +137.7% time, +144 B |
| Disabled capture, two strings | 6.61 ns, 40 B | 6.24 ns, 0 B | -5.6% time, -40 B |
| Capture and first `Message` read | 118.38 ns, 304 B | 180.04 ns, 448 B | +52.1% time, +144 B |
| Node serialization | 1.00x | 1.209x | +20.9% time, +42 first-event bytes |
| Steady binary-log write | 1.00x | 1.220x | +22.0% time, +41 first-event bytes |

The disabled structured path also suppresses interpolation-hole evaluation.
The classic call evaluates every argument before `LogMessage` can filter it.

Capture and materialization are separate costs.
Logger code that groups by `OriginalFormat` can avoid materializing `Message`.

Serialization measurements use a two-value event.
String-table reuse changes the cost for repeated events.
Real binary-log results depend on template and value repetition.

The classic composite baseline already separates its format and arguments.
It does not represent eager interpolated calls that produce a different display string for each event.

The current microbenchmarks show prototype overhead.
They do not yet prove the primary binary-log size goal.
The rollout needs end-to-end measurements with repeated templates and varied values.

## Security and privacy

Structured logging does not redact values.
It makes each captured value available separately to every receiving logger.
The binary log also persists those values.

Caller expressions can expose identifier names and dotted member paths.
These names can reveal implementation details even when values appear harmless.

Task authors must not log secrets.
Explicit names do not classify or protect sensitive data.
Existing binary-log access controls remain necessary.

Formatting can run user-defined `ToString()` or `IFormattable.ToString()`.
Enabled messages, warnings, and errors run that code once during capture.
Disabled messages do not run interpolation-hole code.

Transport stores strings only.
Replay does not instantiate value types or run their formatters.
This rule reduces deserialization risk and removes culture-dependent replay behavior.

Future redaction features can inspect named values.
This proposal does not define a redaction policy or a secret marker.

## Alternatives considered

### Eager interpolation to the old overload

This option creates a string before `TaskLoggingHelper` receives the call.
It evaluates disabled holes and loses names.
It does not meet the goals.

### Classic composite formats only

Classic formats preserve lazy arguments.
However, numeric holes do not provide stable field names.
They also require manual migration from readable C# interpolation.

### Microsoft.Extensions.Logging-style handler and state

MSBuild could copy the complete MEL handler and state model.
That model adds concepts such as `EventId` and scopes that this proposal does not need.
Adding the MEL package would also expand the task dependency surface.

The proposal uses only the established original-format convention.

### Dictionary metadata

A dictionary does not preserve repeated-hole occurrence order.
It also hides the direct position relation between template holes and values.
Hashing adds work during capture.

An ordered list preserves the template relation.
Unique names still let a consumer create a lookup.

### Raw objects across process boundaries

Raw objects can be non-serializable or mutable.
Their formatters can produce different results after transport.
Type transport also increases compatibility and security risk.

The proposal formats values once and transports strings.

### Formatting only during rendering

This option reduces enabled capture work.
However, it requires raw object retention and transport.
It can also change culture or formatter behavior between live logging and replay.

The proposal accepts higher capture cost for deterministic transport.

### Public structured event subclasses

Public subclasses would expose implementation and transport details.
Logger authors would need three type checks.
Future event-shape changes would become public API constraints.

The public interface gives one stable contract.
Internal subclasses preserve compatibility with existing logger base types.

### No Change Wave

The feature changes event and binary-log shape after recompilation.
Old readers can reject the file or skip complete events.
A temporary opt-out is necessary for affected tools.

## Testing plan

### API and language behavior

- Compile calls for every handler overload.
- Verify that interpolation selects the handler overload.
- Verify that literals, variables, and composite formats keep existing overloads.
- Test explicit APIs through dynamic dispatch and non-C# call sites.
- Test `params ReadOnlySpan<T>` and `IReadOnlyList<T>` overload selection.
- Test typed-parameter and reserved-value metadata candidates.
- Test reserved-name collisions if metadata uses `values`.
- Verify that every structured warning and error rejects an empty code.
- Test runtime failure behavior against an old Utilities assembly.

### Capture and rendering

- Test identifiers, dotted paths, fallback names, and explicit names.
- Test each candidate duplicate-name policy.
- Test format strings, alignment, escaped braces, null, and empty strings.
- Test current-culture and invariant-culture capture.
- Test localized display text with an invariant template.
- Test malformed templates, count mismatches, and named-list mismatches.
- Verify that `Message` materializes once and preserves structured metadata.

### Filtering and diagnostics

- Verify that disabled messages do not evaluate holes.
- Verify that the disabled path allocates no per-call state.
- Verify warning suppression, warnings-as-messages, and warnings-as-errors.
- Verify codes, help data, locations, importance, timestamps, and task context.

### Transport

- Round-trip all three event kinds through node serialization.
- Route all three event kinds through an out-of-process TaskHost.
- Verify that an out-of-process structured error fails the build.
- Round-trip all three event kinds through binary-log version 28.
- Verify string deduplication and repeated-event size.
- Verify null and empty values remain different.
- Verify that a new reader replays old logs.
- Verify old-reader behavior with and without forward compatibility.
- Verify Change Wave output with an old reader.
- Add mixed-version node coverage after the team selects that contract.

### Performance

- Keep capture, disabled, materialization, node, and binary-log benchmarks separate.
- Track time and allocation changes for zero, one, two, and many holes.
- Compare repeated structured templates with equivalent materialized messages that contain varied values.
- Measure complete production binary logs before and after selected task migrations.
- Treat regressions as review data, not as a public API guarantee.

## Rollout plan

1. Resolve the open API and compatibility questions.
2. Merge the public contract and transport changes behind Change Wave 18.11.
3. Update binary-log consumers before broad task migration.
4. Document the Change Wave and old-reader limitation.
5. Migrate selected built-in task call sites after transport support ships.
6. Measure production binary logs before wider migration.
7. Retire the Change Wave through the normal rotation process.

## Open questions

1. **Explicit name validation:** Should `Named` reject invalid names, or should the format define an escaping rule?
2. **Duplicate names:** Should capture reject duplicates, preserve them, or add deterministic suffixes to names and the template?
3. **Capture culture:** Should capture preserve current-culture display text or store invariant structured values?
4. **Diagnostic metadata placement:** Should code and related metadata use typed parameters or reserved entries in `values`?
5. **Dynamic diagnostic APIs:** Do dynamic callers need overloads for locations, help keywords, and help links?
6. **Old-reader fidelity:** Is skipping complete structured records acceptable for forward-compatible readers?
7. **Dual binary records:** Should version 28 include a classic fallback record for old readers despite size and duplicate-event risks?
8. **Reader defaults:** The implementation defaults `AllowForwardCompatibility` to false, but the Binary Log document describes compatibility mode as the default. Which behavior is authoritative?
9. **Mixed nodes:** Must new nodes downgrade structured events before they communicate with an older node?
10. **Invariant templates:** What validation or guidance ensures that `OriginalFormat` stays stable across localization and refactoring?
11. **Fallback names:** Should fallback numbering start at `Value0` or `Value1`?
12. **Public nullability:** Should the interface permit null after normal construction, or only during deserialization?
13. **Value-count limit:** Should the current 65,535-value transport limit become part of the public contract?
14. **Change Wave scope:** Should the wave control only event transport, or all automatic handler selection behavior?
15. **Rollout dependency:** Which binary-log viewers and internal loggers must support version 28 before the feature ships?
16. **Old-reader matrix:** Which versions of MSBuild, Structured Log Viewer, and internal readers must reject, skip, or display version 28 events?

## Proposed decisions for team review

| Area | Proposed decision |
|---|---|
| Task syntax | Use interpolated string handlers on existing method names |
| Dynamic access | Add explicit named-template methods |
| Diagnostic metadata | Resolve typed parameters versus reserved structured values |
| Structured contract | Expose one public interface |
| Event classes | Keep three dedicated subclasses internal |
| Value representation | Format once, resolve the capture culture, and transport nullable strings |
| Collection shape | Use an ordered list and resolve the duplicate-name policy |
| Display behavior | Materialize `Message` lazily and cache it |
| Warning policy | Capture warnings before engine suppression or promotion |
| Node transport | Add dedicated event identifiers |
| Binary log | Add three version 28 length-prefixed record kinds |
| Compatibility | Gate dedicated events with Change Wave 18.11 |
| Security | Treat names and values as sensitive log content |
