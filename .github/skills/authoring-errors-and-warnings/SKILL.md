---
name: authoring-errors-and-warnings
description: "Author or change MSBuild user-facing errors, warnings, messages, and resource strings. Not general compiler diagnostics, compatibility triage alone, or binary-log format implementation."
argument-hint: "Describe the diagnostic, its caller, and whether behavior changes."
---

# Author MSBuild diagnostics

**Use for:** selecting the resource/helper, code, text, arguments, and location for an MSBuild diagnostic.

**Do not use for:** assigning another product's diagnostic codes, broad review, or deciding compatibility without a concrete diagnostic change.

## Source preflight

Find the actual producer and its neighboring logging calls. Inspect its resource owner: [Tasks Strings.resx](../../../src/Tasks/Resources/Strings.resx), [Engine Strings.resx](../../../src/Build/Resources/Strings.resx), or shared [Framework SR.resx](../../../src/Framework/Resources/SR.resx), as applicable.

Read the [diagnostic reference](references/diagnostic-authoring.md) for code allocation, current helper examples, localization, and paths. Follow the relevant [path instructions](../../instructions) rather than copying their invariants here.

## Workflow

1. Establish the condition and current behavior. For new severity or a newly reached diagnostic, use [compatibility assessment](../assessing-breaking-changes/SKILL.md); "always wrong" does not alone justify a new failure in existing builds.
2. Choose error, warning, or message for the caller's contract and audience. Message importance is not warning severity.
3. Reuse an existing resource when its meaning fits; otherwise allocate within the appropriate code bucket and check live/retired usage. Do not assume every resx has a "next code" counter.
4. Use the owning resource-based helper with the exact argument count. Give enough context and an actionable remedy where one is known; preserve separate file/line metadata.
5. Cover the changed diagnostic's code, arguments, location, and result/promotion behavior. Use the existing localization process when resources change, not a mandatory full build for every diagnostic discussion.

## Evidence and stop condition

Stop when the requested diagnostic contract is implemented or explained with source-backed resource/helper choices and appropriately scoped evidence. Do not invent new warnings to satisfy a checklist, manually author placeholder translations, or expand the work into unrelated message modernization.
