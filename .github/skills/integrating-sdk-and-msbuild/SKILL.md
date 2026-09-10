---
name: integrating-sdk-and-msbuild
description: "Investigate or change a concrete MSBuild/SDK/NuGet boundary: imports, restore evaluations, project-reference contracts, or host-specific target behavior. Not ordinary target customization or automatic multi-repository coordination."
argument-hint: "Identify the host, project/SDK, properties or targets, and observed boundary failure."
---

# Work across the SDK/MSBuild boundary

**Use for:** determining which producer/consumer owns a property, import, restore/build transition, reference negotiation, or CLI/IDE target contract.

**Do not use for:** turning every props/targets edit into an SDK investigation, general CI diagnosis, or automatically filing issues in multiple repositories.

## Source preflight

Establish the actual SDK, MSBuild binaries/host, project shape, requested targets, and global properties. [global.json](../../../global.json) describes this checkout's SDK selection, not necessarily the host that produced a supplied log.

Start with the affected imports in [Common props](../../../src/Tasks/Microsoft.Common.props), [Common targets](../../../src/Tasks/Microsoft.Common.targets), or [Common CurrentVersion targets](../../../src/Tasks/Microsoft.Common.CurrentVersion.targets). Inspect the selected SDK's implementation when the boundary leaves this repository.

Follow [evaluation instructions](../../instructions/evaluation.instructions.md) or [target instructions](../../instructions/targets.instructions.md) for the affected code.

## Workflow

1. State the observed producer/consumer mismatch and choose one primary trace: [evaluation/restore](references/evaluation-and-restore.md) or [references/target contracts](references/project-references-and-targets.md).
2. Trace the actual nested imports, global-property sets, or reference metadata. Do not infer ordering from a flattened SDK diagram.
3. Compare relevant CLI/IDE or outer/inner-build branches. Establish the required contract before changing defaults or hooks.
4. If a cross-repository change is necessary, identify compatible sequencing and consumption evidence. Do not assume MSBuild must always land first or that diagnosis authorizes external posting.
5. Select a focused reproduction or existing integration evidence; use [bootstrap guidance](../use-bootstrap-msbuild/SKILL.md) only when locally built behavior must be exercised.

## Evidence and stop condition

Stop when the responsible boundary, old/new contract, and scoped evidence or blocker are clear. A property/import explanation does not require builds in multiple repositories. Do not claim all design-time or multi-targeting scenarios are covered from a single CLI build.
