---
name: assessing-breaking-changes
description: "Assess compatibility risk in changes to existing MSBuild behavior, diagnostics, defaults, or public contracts. Not ChangeWave implementation or a mandatory review of semantics-preserving refactoring."
argument-hint: "Describe the old and proposed behavior and affected consumers."
---

# Assess MSBuild compatibility

**Use for:** deciding whether an observable change is acceptable, needs an opt-in or temporary opt-out, or requires a migration plan.

**Do not use for:** implementing an already-decided wave, assigning diagnostic codes, or expanding an internal refactor into a full compatibility matrix.

## Source preflight

Identify the baseline, changed behavior, callers, and supported hosts/TFMs. Read the [instructions for the affected paths](../../instructions) and the current [framework configuration](../../../src/Directory.Build.props).

For diagnostic changes, inspect the actual [warning-promotion logic](../../../src/Build/BackEnd/Components/Logging/LoggingService.cs), not just similarly named project properties. For an opt-out, inspect [ChangeWaves](../../../src/Framework/ChangeWaves.cs).

## Workflow

1. Describe a concrete existing build or consumer that can observe the difference: success/failure, outputs, ordering, API availability, logs, or performance.
2. Separate existing behavior from genuinely new opt-in functionality. A bug fix still needs a blast-radius assessment; not every bug fix needs a wave.
3. Apply the relevant part of [compatibility decisions](references/compatibility-decisions.md). New warnings can fail warning-as-error builds; changing a warning into an error is not a compatibility workaround.
4. Choose the mitigation and evidence needed for the affected boundary. A ChangeWave is default-on risk management, not proof that existing builds remain unchanged.
5. Record the decision and remaining uncertainty. Use [changewaves](../changewaves/SKILL.md) only for implementation, or [diagnostic authoring](../authoring-errors-and-warnings/SKILL.md) for the selected diagnostic.

## Evidence and stop condition

Stop when the observable difference, affected consumers, compatibility decision, and scoped evidence are clear. An assessment-only request does not authorize code changes or executing a broad test matrix. For a fix, require evidence for the changed contract rather than claiming all builds are compatible.
