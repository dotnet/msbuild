---
name: benchmarking-msbuild
description: "Measure and compare MSBuild modes, SDKs, or revisions with an explicit warm/cold and clean/incremental/no-op experiment. Not speculative optimization, CI failure triage, or proof from a single elapsed-time sample."
---

# Compare MSBuild performance

## Before timing

Define the comparison, workload, changing variable, output artifact, and measurement budget. Read [the experiment protocol](references/experiment-protocol.md) for the required controls.

Do not infer `-mt` from a discussion about migrated tasks, or a warm process from an incremental build. Confirm those settings explicitly in the experiment record before spending time on rounds.

## Workflow

1. Pin A/B executable and SDK identities, source revisions where applicable, and the workload.
2. Separate clean, source-edit incremental, and no-op builds; separate process/server warmth from filesystem/compiler caches.
3. Prove the commands succeed and actually use the intended execution modes. Diagnose setup failure before timing.
4. Run paired interleaved rounds with the same controls, logging settings, and declared reset/warm-up procedure. Do not run unrelated heavy work concurrently with timings.
5. Inspect spread and paired differences, not just one fastest result. Use a diagnostic run to investigate a reproducible difference without silently changing the timed configuration.
6. Return measured results, commands/identities, artifacts, and uncertainty. Stop at the declared budget or a concrete setup blocker; do not keep sampling until a desired result appears.

Use [bootstrap MSBuild](../use-bootstrap-msbuild/SKILL.md) only when local product revisions are the comparison, and [performance guidance](../optimizing-msbuild-performance/SKILL.md) after evidence identifies an implementation bottleneck.

Do not alter Defender, indexing, system-wide caches, other users' processes, or machine security settings as an unrequested benchmark optimization.
