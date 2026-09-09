# Diagnostic authoring reference

## Severity and audience

First distinguish a user input/build condition from an internal programming failure. [ProjectFileErrorUtilities](../../../../src/Shared/ProjectFileErrorUtilities.cs) explicitly warns against using project-file errors in place of internal invariant checks.

For an existing build path, consider compatibility before introducing any new error or warning. A default-on ChangeWave is a temporary opt-out, not immunity from warning-as-error breaks. The [compatibility reference](../../assessing-breaking-changes/references/compatibility-decisions.md) owns that decision and the engine/compiler warning-property distinctions.

For genuinely new opt-in behavior, errors can reject invalid input and warnings can identify actionable risk. Use messages for informational progress or diagnostic detail. `MessageImportance` has High, Normal, and Low; `LoggerVerbosity.Diagnostic` is a different concept.

Useful diagnostic text explains the problem, the relevant context, and a remedy when known. Do not fabricate a remedy or force every short diagnostic into three sentences. SDK selection, target frameworks, and targeting packs are different concepts; prefer a real nearby resource over an invented "framework is not installed" example.

## Resource ownership and code allocation

Historical code areas are:

| Prefix | Area |
|---|---|
| MSB1xxx | Command-line handling |
| MSB2xxx | Historical conversion component |
| MSB3xxx | Built-in tasks |
| MSB4xxx | Engine |
| MSB5xxx | Shared code |
| MSB6xxx | Utilities |

These areas do not imply that every resource physically resides in that assembly today. Shared resources are in [Framework SR.resx](../../../../src/Framework/Resources/SR.resx), which includes diagnostics used by other assemblies.

1. Find the caller's resource manager and nearest analogous resource/call.
2. Read that file's allocation information. [Tasks Strings.resx](../../../../src/Tasks/Resources/Strings.resx) has per-task buckets and a bucket table; other files may not have a "Next message code" comment.
3. Search for the proposed code across the repository and available retirement/history information. An absent live string does not prove that a shipped code can be reused.
4. Stay within the allocated area. If a task bucket is exhausted, follow the allocation/overflow process rather than borrowing another task's number.
5. Update an existing allocation index when applicable. Do not invent or rewrite an unrelated index just because a generic recipe expects one.

[Assigning MSB error codes](../../../../documentation/assigning-msb-error-code.md) supplies the allocation background. Verify its examples and index descriptions against the actual current resource file.

Match resource-key naming and ordering in the affected area. Dotted task keys such as `Copy.Error` are common; engine keys are not all dotted.

## Resource and call-site examples

This is the existing Copy diagnostic shape, not a code available for reassignment:

```xml
<data name="Copy.Error">
  <value>MSB3021: Unable to copy file "{0}" to "{1}". {2}</value>
  <comment>{StrBegin="MSB3021: "}</comment>
</data>
```

Call-site fragments following the resource-based helpers:

```csharp
Log.LogErrorWithCodeFromResources(
    "Copy.Error", sourceFile, destinationFile, exception.Message);

Log.LogWarningWithCodeFromResources(
    "ResolveAssemblyReference.FoundConflicts", assemblyName, conflictDetails);

ProjectFileErrorUtilities.ThrowInvalidProjectFile(
    new BuildEventFileInfo(elementLocation),
    "InvalidProjectFile",
    exception.Message);
```

The warning resource has **two** placeholders. The engine's `InvalidProjectFile` resource has **one**, and its file location is supplied separately. These are examples for their respective resource owners, not interchangeable calls from any logger.

Sources: [Tasks resources](../../../../src/Tasks/Resources/Strings.resx), [Engine resources](../../../../src/Build/Resources/Strings.resx), [ProjectFileErrorUtilities](../../../../src/Shared/ProjectFileErrorUtilities.cs), and [BuildEventFileInfo](../../../../src/Shared/BuildEventFileInfo.cs).

Use resource-based formatting for localizable diagnostic text rather than ad hoc concatenation. For a new resource, make placeholder meanings and non-translatable tokens clear to translators. Keep `{StrBegin="MSBxxxx: "}` markers consistent with neighboring coded resources; replace the schematic code with the allocated one. Do not document placeholders that are absent from the value.

## Paths and locations

Separate the structured location from paths appearing as message arguments.

- When an `IElementLocation` exists, construct `BuildEventFileInfo` from it so file, line, and column reach loggers and IDE navigation.
- Match the existing path contract. Relative paths can be ambiguous across project references; normalizing or changing user-supplied path spelling can also change diagnostic output.
- Preserve the appropriate escaped/unescaped representation at the helper boundary.
- Quote path arguments consistently with the existing resources, especially when spaces are possible.

Do not impose a universal "always relative" or "never normalize" rule. The `InvalidProjectFile` resource itself explains why the project filename is supplied separately to loggers.

## Localization and evidence

[Localization.md](../../../../documentation/wiki/Localization.md) describes neutral resources, XLF files, generated localized resources, and translation ownership. Ordinary diagnostic edits belong in the neutral resx; use the repository's localization generation flow for placeholder updates. A translation correction is a different task with its own process.

Select evidence for the change: code/severity, argument formatting, location, build result, warning promotion, or message importance. Full localized-text assertions are useful only where that text is the intended contract; avoid making a locale-dependent assertion stand in for a stable code/location assertion.

Follow [running-unit-tests](../../running-unit-tests/SKILL.md) for runner selection. Resource changes may need generated localization updates, but an explanation or Markdown-only diagnostic audit does not require a product build.
