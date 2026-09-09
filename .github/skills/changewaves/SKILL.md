---
name: changewaves
description: "Implement an already-justified MSBuild ChangeWave, inspect an opt-out, or retire a wave when explicitly requested. Not a requirement to gate every behavioral fix or to rotate waves during unrelated work."
argument-hint: "Describe the feature gate, opt-out behavior, or requested retirement."
---

# Manage MSBuild ChangeWaves

**Use for:** selecting/registering the release's wave, conditioning a feature, proving opt-out behavior, or an authorized retirement.

**Do not use for:** automatic retirement, a new feature already isolated by an explicit opt-in, or deciding compatibility without first using [assessing-breaking-changes](../assessing-breaking-changes/SKILL.md).

## Source preflight

Read [eng/Versions.props](../../../eng/Versions.props) for the development version and [ChangeWaves.cs](../../../src/Framework/ChangeWaves.cs) for named fields, ordered `AllWaves`, and cached opt-out state. Do not copy a version number from a skill or old example.

Inspect the affected caller and [current wave documentation](../../../documentation/wiki/ChangeWaves.md). Follow [framework instructions](../../instructions/framework.instructions.md) when changing the wave registry.

## Workflow

1. Use the development release's existing named wave; add one only if the justified feature requires it and that wave is absent.
2. Gate the changed behavior with `AreFeaturesEnabled`, preserving the old path. Use the feature's named wave, not a moving `HighestWave` lookup.
3. Cover the new behavior and the same scenario with the wave disabled. Follow the [lifecycle reference](references/lifecycle.md) for process-cached state and test isolation.
4. Add the feature to the matching current-rotation heading in `ChangeWaves.md`; use the real PR link when available.
5. Retire a wave only when requested, following the separate retirement procedure and checking affected callers.

## Evidence and stop condition

Stop after the requested gate or retirement, its scoped evidence, and documentation are complete. An unset variable enables waves by default; a successful default-path test does not prove opt-out. Do not sweep unrelated waves, invent release-retention promises, or require a full build for a wave-documentation question.
