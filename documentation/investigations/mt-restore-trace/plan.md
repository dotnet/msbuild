# Plan: restore-warm ETL comparison for multi-proc vs multi-threaded MSBuild

## Goal

Use the two ETL traces as evidence to explain why the **multi-threaded** restore is slower than the comparable **multi-proc** restore, with the current emphasis on the **evaluation phase** and the previously identified shared-state bottleneck candidates.

Inputs:

- `C:\repro\perf\msbuild-mt\restore-warm\restore-warm.etl`
- `C:\repro\perf\msbuild-mt\restore-warm-mt\restore-warm-mt.etl`

Supporting references:

- Existing source-scan bottleneck catalog: `documentation\investigations\parallel-bottlenecks\`
- MSBuild EventSource reference from PR #13961: `documentation\specs\event-source.md`

## Trace interpretation constraints

- The two traces are **not** whole-build equivalents.
- `restore-warm.etl` is from **one worker node process** in a **multi-proc** build, so it captures only the work seen by that node.
- `restore-warm-mt.etl` is from the **single process** running the whole build in **multi-threaded** mode.
- Therefore:
  - do **not** compare whole-trace duration directly;
  - do **not** compare whole-trace event counts directly;
  - only compare work after normalizing to the **same project**, **same phase**, and ideally the same repeated operation shape.

## Investigation shape

This investigation will run in phases.

### Phase 1 - Trace reconnaissance

Purpose: learn what these ETLs actually contain and what evidence can support a fair phase-2 comparison.

Questions Phase 1 must answer:

1. What providers and major event families are present in each trace?
2. What `Microsoft-Build` EventSource events are visible, especially evaluation-related events such as:
   - `Evaluate`
   - `EvaluateCondition`
   - `Parse`
   - `LoadDocument`
   - `ExpandGlob`
   - `ProjectGraphConstruction`
   - SDK resolver events when they affect restore evaluation
3. What fields can be used to align equivalent work across traces?
   - project path / file
   - node or process identity
   - activity IDs / related activity IDs
   - event names and payload fields
   - relative timing windows
4. What useful detail is available for deeper bottleneck analysis?
   - start/stop pairs and durations
   - evidence of waits, fan-in, or serialized regions
   - process/thread distribution
   - file/parse/import activity
5. What limitations must phase 2 respect?
   - ETL indexing truncation
   - missing parser-specific/kernel coverage in the MCP
   - topology mismatch between traces

Outputs from Phase 1:

- a short reconnaissance summary of what is present in the traces;
- a list of fields/signals that can anchor equivalent comparisons;
- a concrete Phase 2 plan focused on the most promising evaluation-side comparison method.

### Phase 2 - Equivalent-scope comparison

Purpose: compare one or more equivalent project/phase slices between the traces and test the bottleneck theory.

Expected shape (to be refined after Phase 1):

1. Choose one or more projects that clearly appear in both traces.
2. Align evaluation work for those projects.
3. Compare where time expands in MT mode:
   - more evaluation time inside the same EventSource operation;
   - more repeated evaluation-related sub-operations;
   - more concurrency on shared cache / parse / condition paths;
   - more scheduler or coordination interference leaking into evaluation.
4. Map any observed hotspot back to the existing bottleneck candidates.
5. If the evidence points elsewhere, capture that explicitly instead of forcing a bottleneck match.

## Phase 1 execution checklist

1. Open both traces in the nettrace MCP.
2. Inventory top providers and trace metadata.
3. Describe `Microsoft-Build` schema in both traces.
4. Identify evaluation-related event types and their payload fields.
5. Pull representative sample events for likely anchor operations.
6. Determine the best alignment keys for cross-trace comparison.
7. Write the reconnaissance summary and replace the placeholder Phase 2 outline with a concrete next-step plan.

## Assumptions

- The current pass is trace analysis only; no code changes are expected from this phase.
- The nettrace MCP is the primary inspection tool, but its ETL indexing limitations must be documented when they affect conclusions.
- Phase 1 should stay lightweight and produce just enough structure to make Phase 2 targeted rather than exploratory.
