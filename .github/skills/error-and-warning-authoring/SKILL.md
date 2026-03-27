---
name: authoring-errors-and-warnings
description: 'Guides authoring of MSBuild errors, warnings, and diagnostic messages. Consult when adding new MSBxxxx codes, writing or modifying user-facing diagnostic text, deciding between error/warning/message severity, working with Strings.resx resource files, formatting paths in error output, or evaluating whether a new warning could break WarnAsError builds.'
argument-hint: 'Describe the error/warning being added or modified.'
---

# Error and Warning Authoring in MSBuild

Error messages are MSBuild's primary user interface. They must help developers fix problems without reading source code.

For the mechanics of error code assignment, see [assigning-msb-error-code.md](../../../documentation/assigning-msb-error-code.md).

## Error Message Quality Rules

Every error message must answer three questions:

1. **What happened?** — State the problem clearly
2. **Why?** — Provide context (file, property, expected value)
3. **What should the user do?** — Give actionable guidance

```xml
<!-- BAD: What is "it"? What should I do? -->
<value>MSB4999: Invalid configuration.</value>

<!-- GOOD: States the problem, context, and fix -->
<value>MSB4999: The project "{0}" specifies TargetFramework "{1}" which is not installed. Install the SDK for "{1}" or update the TargetFramework in the project file.</value>
```

## Severity Decision Framework

```
Is the condition always wrong (invalid input, impossible state)?
├── Yes → ERROR (build should fail)
└── No
    ├── Could this cause subtle build correctness issues?
    │   ├── Yes, likely → WARNING (but see WarnAsError impact below)
    │   └── Yes, maybe → MESSAGE at Normal importance
    └── Is this purely informational?
        └── Yes → MESSAGE at Low importance
```

### The WarnAsError Constraint

**New warnings are breaking changes** for builds using:
- `-WarnAsError` (CLI)
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (project)
- `<WarningsAsErrors>MSBxxxx</WarningsAsErrors>` (specific codes)

Before adding a new warning, consider:
1. **Is the warning important enough to justify breaking WarnAsError builds?** If yes, gate behind a ChangeWave (see [changewaves skill](../changewaves/SKILL.md)).
2. **Could this be a Message instead?** Messages never break builds but still appear in binary logs.
3. **Should this be an error from the start?** If the condition is always wrong, skip the warning stage.

## MSB Error Code Assignment

### Code Ranges

| Range | Area |
|-------|------|
| `MSB1xxx` | Command-line handling |
| `MSB3xxx` | Tasks (`Microsoft.Build.Tasks.Core.dll`) |
| `MSB4xxx` | Engine (`Microsoft.Build.dll`) |
| `MSB5xxx` | Shared code across assemblies |
| `MSB6xxx` | Utilities (`Microsoft.Build.Utilities`) |

### Assignment Process

1. Open the `Strings.resx` file for the appropriate assembly
2. Find the "Next message code" comment at the bottom of the file
3. Search the repo to confirm the code is not already in use
4. Use the code; update the comment to the next available number

See [assigning-msb-error-code.md](../../../documentation/assigning-msb-error-code.md) for detailed steps.

## Resource String Format

```xml
<data name="FeatureArea.DescriptiveName">
  <value>MSBxxxx: Clear description with {0} placeholders for runtime values.</value>
  <comment>{StrBegin="MSBxxxx: "}{0} is the project file path. {1} is the property name.</comment>
</data>
```

### Rules

- Resource name uses `FeatureArea.DescriptiveName` convention
- Value starts with `MSBxxxx: ` (code, colon, space)
- Comment documents the `{StrBegin}` marker and explains each `{N}` placeholder
- Comments guide translators — mention untranslatable tokens

## Consuming Error Resources in Code

Use the standard formatting method that extracts and applies the error code:

```csharp
// For errors
Log.LogErrorWithCodeFromResources("Copy.Error", sourceFile, destFile, ex.Message);

// For warnings
Log.LogWarningWithCodeFromResources("ResolveAssemblyReference.Conflict", assemblyName);

// For engine-level errors (not in tasks)
ProjectFileErrorUtilities.ThrowInvalidProjectFile(
    elementLocation,
    "InvalidProjectFile",
    arg1, arg2);
```

**Never** construct error messages by string concatenation — always use resource strings for localization support.

## Localization Requirements

- Error **codes** (`MSBxxxx`) are never localized
- Error **text** is localized into all supported languages
- After adding/modifying resource strings, run a full build to generate `.xlf` placeholder translations
- Use `<comment>` elements to help translators understand context
- See [Localization.md](../../../documentation/wiki/Localization.md) for the full localization process

## Path Formatting in Error Messages

- Show paths relative to the project directory when possible
- Use the path exactly as the user specified it (don't normalize slashes)
- Include file and line number via `IElementLocation` when available — this enables IDE click-to-navigate
- For paths that could contain spaces, ensure they're quoted in the message

## Checklist for New Errors/Warnings

- [ ] Error code assigned from correct `Strings.resx` range
- [ ] "Next message code" comment updated in the resx
- [ ] Message answers: what happened, why, what to do
- [ ] `{StrBegin}` comment and placeholder documentation added
- [ ] Severity chosen (error vs warning vs message) with WarnAsError considered
- [ ] If warning: ChangeWave gating evaluated
- [ ] Full build run to generate `.xlf` files
- [ ] Test verifies the error is produced with correct code and text
