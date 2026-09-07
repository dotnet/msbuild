---
name: multithreaded-task-migration
description: "Migrate or assess an MSBuild task's multithreaded support while preserving its observable contract. Use for task changes involving MSBuildMultiThreadableTask, IMultiThreadableTask, or TaskEnvironment. Not generic thread-safety work, build benchmarking, CI triage, or agent-context maintenance."
---

# Migrate an MSBuild task to MT support

## Use and authority

Use for a specific task migration or its compatibility assessment. A review stays read-only unless edits are requested. This skill does not authorize publication, thread changes, installation, framework upgrades, or an unrelated modernization pass.

## Source preflight

1. Identify the task-authoring repository, revision/diff, affected concrete classes and bases, callers, and public inputs/outputs.
2. Resolve the actual Framework/Utilities and host/factory contracts. Attribute routing, interface/property injection, constructor injection, and direct construction are separate questions.
3. Read the checkout's SDK/framework references, task/test project TFMs, runner configuration, and existing fixtures. Do not assume a fixed TFM pair, helper implementation, or test-command syntax.
4. Establish the allowed work and required proof. Reuse prior review/evidence for the same revision; discover capabilities rather than requiring an installed reviewer or analyzer.

## Workflow

1. Map the relevant call chains, including constructors/initializers, unannotated bases, ToolTask overrides, nested tasks, callbacks, and interface/delegate boundaries.
2. Establish the baseline contract for reachable path/env inputs, diagnostics, exception handling, outputs, child processes, and shared-state lifetimes.
3. Make a scoped migration when authorized: use the supplied environment at the appropriate lifecycle point, resolve filesystem inputs without changing user-facing values, and preserve existing failure semantics.
4. Address shared state by its actual key, ownership, lifetime, and concurrency contract. An API-name match is a prompt to investigate, not automatically a defect.
5. Assess whether tests distinguish the changed behavior. Use the existing runner and isolation mechanisms; use an engine-host scenario when the claim concerns factory injection or MT routing.
6. Retain an unannotated/isolated design when shared-process safety cannot be supported. Explain the concrete constraint; do not equate annotation count with completion.

## Evidence and stop

Give each affected boundary a disposition: preserved by source reasoning, demonstrated by a targeted scenario, intentionally changed with authority, or unresolved. Record the actual host/mode for execution evidence. A passing analyzer, a faster build, or `Execute()` returning true is not proof of every migration requirement.

Stop once the requested scope has evidence-backed dispositions and the report states remaining limitations. Do not require fixed review loops, a general-review swarm, both runtimes regardless of support, or a full build for every change.

## Focused references

Read only the needed reference; do not recursively preload the set.

| Question | Reference |
| --- | --- |
| Which attribute/interface, injection path, defaults, or environment semantics apply? | [Source and migration](references/source-and-migration.md) |
| How do paths, diagnostics, canonicalization, and the eight compatibility hazards interact? | [Path and diagnostic compatibility](references/path-and-diagnostic-compatibility.md) |
| What can bases, abstractions, caches, registered objects, or analyzer limits hide? | [Call chains and shared state](references/call-chains-and-shared-state.md) |
| What does ToolTask already do, and what must an override preserve? | [ToolTask and processes](references/tooltask-and-processes.md) |
| Which regression assertions, host evidence, and review conclusions are justified? | [Validation and review](references/validation-and-review.md) |
