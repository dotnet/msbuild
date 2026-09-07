---
name: optimizing-msbuild-performance
description: "Investigate or optimize a measured MSBuild engine hot path, allocation pattern, or performance regression. Not every Span/LINQ edit, generic slow-project diagnosis, or automatic benchmark/agent orchestration."
argument-hint: "Describe the workload, measured bottleneck or hypothesis, runtime, and success metric."
---

# Optimize engine performance

**Use for:** an engine implementation hypothesis or measured regression in evaluation, expansion, execution, collections, logging, or I/O.

**Do not use for:** mechanically replacing language constructs, treating all caches as safe, or expanding a small source question into a benchmark campaign. Use [benchmarking-msbuild](../benchmarking-msbuild/SKILL.md) for an actual comparison experiment.

## Source preflight

Identify workload, binaries, runtime/TFM, build mode, cache state, and metric. Distinguish measured results from a source-derived hypothesis. Read current [framework configuration](../../../src/Directory.Build.props) and the affected [path instructions](../../instructions).

Start at the measured operation and its callers. [Engine hot-path guidance](references/engine-hot-paths.md) maps relevant implementations and runtime-sensitive tradeoffs; read only the part needed for the hypothesis.

## Workflow

1. Establish the baseline and bottleneck using existing measurements, the [evaluation profiler](../../../documentation/evaluation-profiling.md), or an appropriate existing profiling/benchmark workflow.
2. Identify the actual mechanism: repeated work/I/O, allocation/capture/boxing, lookup cost, synchronization, or retained state.
3. Preserve semantics, ordering, comparison, lifetime, and concurrency while making the smallest coherent optimization.
4. Compare the relevant workload/metric under controlled conditions. Check affected runtime/TFM paths; JIT behavior and API availability need not match.
5. Report the measured effect and tradeoffs, or label the proposal unmeasured. Load another workflow only for a concrete uncovered boundary.

## Evidence and stop condition

Stop when evidence answers the scoped performance question or identifies the missing measurement. A faster-looking construct, fixed collection-size threshold, or one successful build is not a performance result. Do not run unrelated full suites or claim every workload improved.
