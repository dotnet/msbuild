# Plan: AI-assisted source scan for in-process parallel-build bottlenecks

## Goal

Identify source-level bottlenecks that could limit throughput when **one MSBuild process builds multiple projects in parallel**, with a focus on **shared process-wide state** rather than task implementation details.

This document is intentionally a **plan only**. It describes what to look for, how to split the work, and how to collect/report findings later. It does **not** record scan results yet.

The first execution pass will focus on the engine/framework/shared layers. The broader story may include additional host-side and orchestration code later, but that expansion is intentionally deferred until the first pass shows whether it is needed.

## What to look for

### Primary signals

- static mutable fields
- singleton instances
- process-wide caches or registries
- shared maps or sets used across projects
- lazy-initialized global services
- any shared state protected by `lock`, `Monitor`, `Mutex`, `SemaphoreSlim`, `ReaderWriterLockSlim`, or equivalent synchronization
- shared state that triggers expensive work such as:
  - XML parsing
  - filesystem enumeration
  - environment or registry access
  - SDK / toolset discovery
  - serialization or translation
  - event formatting / logging fan-in

### Non-goals for this pass

- Task implementation bottlenecks
- General per-project hot code that is not shared across projects
- Pure constants or simple immutable values with no significant work behind them

## Stage model for classifying findings

1. **Entry / configuration setup**  
   BuildManager startup, config registration, toolset/environment/bootstrap state.
2. **Project load and evaluation**  
   ProjectCollection, XML caching, imports, properties/items expansion, evaluation caches.
3. **Scheduling / execution coordination**  
   request scheduling, result/config caches, node/build coordination, cross-request shared state.
4. **Logging / event forwarding**  
   central loggers, event queues, formatting, serialization, forwarding.
5. **Cross-cutting framework services**  
   traits, tool location, SDK/toolset discovery, file/path/shared utility caches that many stages depend on.

## Candidate triage rubric

Each candidate should be captured with:

1. **Shared object** - symbol/type/field and why it is process-wide or effectively shared.
2. **Evidence** - file and line references for the shared state, synchronization, and expensive work.
3. **Why it could bottleneck** - expected contention mode under many parallel projects.
4. **Likelihood** - `high`, `medium`, or `low`.
5. **Impact shape** - startup burst, repeated per-project contention, long critical section, fan-in/fan-out, cache stampede, or IO serialization.
6. **Stage** - one of the stage buckets above.
7. **Next step** - deeper validation to confirm or dismiss the concern.

## Subagent strategy

Use at most **4 concurrent subagents** to avoid overloading the machine.

### Wave 1: broad scan

Run four source-scan agents in parallel with non-overlapping scopes:

1. **Evaluation agent**  
   Search `src/Build/Evaluation/**` and tightly-coupled evaluation helpers.
2. **Execution agent**  
   Search `src/Build/BackEnd/**` and scheduler / BuildManager / result-cache plumbing.
3. **Logging agent**  
   Search `src/Build/Logging/**` plus direct event-flow plumbing.
4. **Framework agent**  
   Search `src/Framework/**`, `src/Shared/**`, and other global helpers used across stages.

### Wave 2: candidate deep dives

After Wave 1 produces the candidate list:

1. Normalize duplicates into one canonical candidate per shared object.
2. Rank by likely impact and evidence strength.
3. Launch follow-up subagents in batches of up to 4, one candidate per agent.
4. Each deep-dive agent should answer:
   - What code paths hit this shared state?
   - How often is it likely to be touched in a multi-project build?
   - Is the shared state read-mostly, write-heavy, or mixed?
   - Is synchronization narrow and cheap, or could it serialize meaningful work?
   - Is the expensive work inside or outside the critical section?
   - What evidence would move the candidate up or down?

### Consolidation

- Keep **all** plausible candidates in `findings.md`, even low-likelihood ones.
- Each subagent should produce a **logbook file** capturing the important findings, relevant context, and why the candidate was or was not escalated.
- Create one detailed report per candidate only when the evidence suggests a likely real bottleneck or the case remains important enough to justify a deeper writeup.
- If a candidate looks unlikely to matter after investigation, the logbook can end with a short summary explaining why it is probably not a meaningful bottleneck instead of producing a full report.
- Keep `summary.md` short and focused on the highest-impact findings and why they matter.

## Output structure

- `findings.md` should be the complete catalog.
- `logbooks\*.md` should capture the working notes and conclusions from each subagent investigation.
- `reports\*.md` should hold detailed, evidence-based writeups.
- `summary.md` should identify the few findings most likely to matter in practice, grouped by stage with a short cross-stage top-findings section at the end.

## Assumptions

- This plan assumes the first pass is static-source analysis only.
- It is acceptable to record uncertain candidates now and refine them later.
- The first execution pass will create the findings catalog and per-bottleneck reports only after this plan is approved.
- This phase stays read-only and source-based. Profiling or runtime validation can follow in a later phase if the source scan identifies strong candidates that need confirmation.
