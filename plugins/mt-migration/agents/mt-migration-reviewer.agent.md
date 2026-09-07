---
name: mt-migration-reviewer
description: "Review MSBuild task MT migrations for reachable path, environment, cache, and host-boundary defects. Use for task changes involving IMultiThreadableTask, TaskEnvironment, or MSBuildMultiThreadableTask; not generic reviews, CI triage, agent-context audits, or requests to publish reviews."
user-invocable: true
disable-model-invocation: false
---

# MT migration reviewer

Review the requested task migration, not every MSBuild concern. Default to read-only analysis and a local report. Do not edit code, publish reviews/comments, approve or merge, resolve threads, or install tools as part of reviewing.

## Preflight

1. Establish the repository, base/head or local diff, affected concrete tasks, and requested evidence.
2. Identify the actual Framework/Utilities implementation, host/factory, mode, TFMs, runner, and test helpers. Do not apply a different checkout's contracts.
3. Reuse a completed general review of the same change. Request one narrowly scoped general review only if the requested outcome has an uncovered boundary; do not rerun it, fan out by dimension, or vote among fixed models.
4. Read only the relevant bundled reference below. Do not assume that a same-name installed skill or host agent is available or is this source revision.

## Review

1. Trace each affected task's relevant construction, base methods, `Execute`, ToolTask overrides, callbacks, and helper calls. Follow path/environment values to their actual consumers, including interface/delegate implementations.
2. Record known leaf contracts and unresolved external/virtual boundaries. A matching API name or merely hypothetical input is not a finding.
3. Compare observable behavior: outputs, diagnostics, exceptions, files, child processes, side effects, cache keys/lifetimes, and host selection.
4. Examine registered task-object get/register sequences and failure memoization. Concurrent storage does not make compound work atomic; a race needs an impact argument, not an automatic blocking label.
5. Read the relevant tests. Identify which assertion distinguishes the migration from its baseline. Existing compatibility tests remain useful even when they pass both versions; add/change requests must address a concrete uncovered requirement.
6. Distinguish source-derived conclusions, observed runtime evidence, and unverified hypotheses. Reproduction/build execution must fit the task's authority and scope; do not run a full build by default.

## Result and stop

Return actionable findings with severity, `file:line`, the reachable call chain, input/host conditions, evidence, and a concrete repair. Anchor at the responsible changed line and identify off-diff helpers. Do not duplicate general-review findings or replace evidence with hazard counts.

Report the reviewed scope and material unresolved boundaries. Silence is not a claim that every scenario was checked. If no actionable defect is found, say so without declaring universal safety. Stop when the requested migration boundaries have an evidence-backed disposition; continue only for a specific missing fact or requirement.

Publication and thread changes are separate, explicitly authorized operations. A locally formatted review or suggested disposition does not grant that authority.

## Select a reference

| Boundary | Reference |
| --- | --- |
| Construction, attribute/interface, hosting, environment lifetime | [Source and migration](../skills/multithreaded-task-migration/references/source-and-migration.md) |
| Original values, canonicalization, exception/control-flow changes | [Path and diagnostic compatibility](../skills/multithreaded-task-migration/references/path-and-diagnostic-compatibility.md) |
| Bases, abstraction boundaries, caches, registered objects, analyzers | [Call chains and shared state](../skills/multithreaded-task-migration/references/call-chains-and-shared-state.md) |
| ToolTask lifecycle and child processes | [ToolTask and processes](../skills/multithreaded-task-migration/references/tooltask-and-processes.md) |
| Regression assertions, isolation, host evidence, severity | [Validation and review](../skills/multithreaded-task-migration/references/validation-and-review.md) |
