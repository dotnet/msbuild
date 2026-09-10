# Engine hot-path guidance

## Establish the mechanism

Use [benchmarking-msbuild](../../benchmarking-msbuild/SKILL.md) for experiment design, binary identity, and controlled comparisons. The [evaluation profiler](../../../../documentation/evaluation-profiling.md) can identify expensive evaluation locations/passes. Existing traces can distinguish CPU, allocation, I/O, and waiting.

[General performance goals](../../../../documentation/specs/proposed/General_perf_onepager.md) is background, not an executable profiling recipe or a measurement baseline.

Useful source starting points:

| Area | Implementation |
|---|---|
| Expansion and property functions | [Expander](../../../../src/Build/Evaluation/Expander.cs), [Expander.Function](../../../../src/Build/Evaluation/Expander.Function.cs), [WellKnownFunctions](../../../../src/Build/Evaluation/Expander/WellKnownFunctions.cs) |
| Evaluation | [Evaluator](../../../../src/Build/Evaluation/Evaluator.cs) |
| Lazy item operations | [LazyItemEvaluator](../../../../src/Build/Evaluation/LazyItemEvaluator.cs) and its partial implementations |
| Glob matching/enumeration | [FileMatcher](../../../../src/Framework/Utilities/FileMatcher.cs), [MSBuildGlob](../../../../src/Build/Globbing/MSBuildGlob.cs) |
| Task parameters | [TaskExecutionHost](../../../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) |
| Temporary buffers | [BufferScope](../../../../src/Framework/Utilities/BufferScope.cs) |

These are potential hot paths, not instructions to profile or edit every file.

## Allocations and iteration

Avoid unnecessary allocation on a demonstrated hot path, but identify what actually allocates:

- `Where`/`Select` commonly create iterator objects; a capturing lambda can allocate a closure. Not every LINQ call creates both, and runtime optimizations differ.
- A `foreach` over a concrete collection can use a struct enumerator; iterating through an interface can box or allocate. For example, [OrderedItemDataCollection.Builder](../../../../src/Build/Evaluation/LazyItemEvaluator.OrderedItemDataCollection.cs) exposes enumerators through interfaces.
- `Enumerable.Count()` does not always enumerate the whole sequence: it can use an `ICollection<T>` count. Prefer an appropriate known `Count`/`Length` property, or an existence operation when that is the actual question. See [the API contract](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.count).
- When replacing iteration, preserve ordering, short-circuiting, side effects, and single/multiple-enumeration behavior.

Do not label a generic loop "zero allocations" without its receiver type, body, and measured/runtime context.

String operations need the same analysis. Slicing via spans can avoid a substring allocation when the consumer only compares/parses. Producing a final string still has a cost; `Concat`, interpolation, and `StringBuilder` are not universal allocation-free replacements for `string.Format`.

Reusing a builder or pooled buffer introduces ownership, reset, retention, and concurrency obligations. Use bounded stack allocation and the existing pool/scope pattern where appropriate; consider element size and stack depth, not an unexplained universal byte limit.

## Comparisons and normalization

Use [MSBuildNameIgnoreCaseComparer](../../../../src/Framework/MSBuildNameIgnoreCaseComparer.cs) for the MSBuild-name contract. It has constrained-name and TFM-specific implementation details; do not substitute a culture-sensitive comparison while optimizing.

For other identifiers and paths, follow the actual consumer's contract and existing helpers. [FileMatcher](../../../../src/Framework/Utilities/FileMatcher.cs) intentionally uses case-insensitive behavior in several cross-platform paths; changing every Linux path comparison to ordinal can change builds.

Avoid allocating a lowercase/uppercase string merely to perform one comparison when a correct comparer can do it directly. Deliberate canonicalization for storage is a different operation; preserve its semantics, escaping, and lifetime.

## Collections and snapshots

| Access pattern | Candidates / considerations |
|---|---|
| Fixed contiguous data | Array, `ImmutableArray<T>`, or a span with valid lifetime |
| Read-mostly keyed lookup | Dictionary with the correct comparer; consider frozen/read-only alternatives only when supported and worthwhile |
| Frequent updates with persistent snapshots | Persistent collections or a snapshot design; account for copying and structural sharing |
| Ordered mutable sequence | List/array or the existing ordered abstraction |
| Small lookup | Compare linear scan and keyed lookup for actual data/access distribution; no universal cutoff |
| Concurrent access | Choose synchronization/publication and ownership before a collection type |

`ImmutableArray<T>` has cheap indexed access and contiguous storage. `ImmutableList<T>` has different indexing/update tradeoffs and can suit persistent snapshots. The engine's [OrderedItemDataCollection](../../../../src/Build/Evaluation/LazyItemEvaluator.OrderedItemDataCollection.cs) uses `ImmutableList` and builders; replacing it mechanically can increase copying or break snapshot expectations.

Check all affected TFMs and package references before introducing a collection/API. Availability on the development runtime does not establish support on every library target.

## Caches and concurrency

Hoist work that is invariant for a loop. For cross-call caching, first establish the correct lifetime and invalidation key: project/evaluation, global properties, filesystem state, environment, toolset, or process.

A field or `Lazy<T>` is not automatically correct. Consider concurrent requests, reentrancy, disposal, negative-result caching, retained memory, and whether a reused process sees changed inputs. Preserve the intended behavior when a cached result becomes stale.

Avoid duplicate dictionary lookups with `TryGetValue` where it preserves semantics. Avoid unnecessary intermediate collection materialization, but do not remove a snapshot required for correctness.

## Evaluation and property functions

Evaluation cost depends on visited files, cache behavior, conditions, glob expansion, I/O, and repeated work, not import depth alone. An import-tree change can also alter properties/items; it is not merely a traversal optimization.

Broad globs can be expensive; inspect actual enumeration roots and pruning. Follow [globbing instructions](../../../instructions/globbing.instructions.md) rather than disabling valid exclusion/pruning optimizations.

Condition short-circuiting can save work when semantics permit reordering. Preserve observable errors and dependencies.

Property functions are not uniformly reflection-interpreted slow paths. [Expander.Function](../../../../src/Build/Evaluation/Expander.Function.cs) dispatches known operations through [WellKnownFunctions](../../../../src/Build/Evaluation/Expander/WellKnownFunctions.cs). Identify the actual selected path before replacing an expression.

## Regex and inlining

Compilation trades initialization cost/code size for execution behavior. [MSBuildGlob](../../../../src/Build/Globbing/MSBuildGlob.cs) intentionally avoids compiled regex on Framework for its workload and enables it on the relevant runtime path. Preserve its caching, culture, and compatibility gates.

Reusing a regex and compiling a regex are different decisions. Do not rewrite every regex loop with `RegexOptions.Compiled` without measuring the affected runtime and startup/steady-state tradeoff.

For very hot small methods, investigate inlining, struct enumerators, or dispatch costs. `AggressiveInlining` is a hint, not proof of an improvement; excess code size or changed JIT behavior can offset the gain.

## Logging and result evidence

Formatting a message or constructing arguments before a filtered call still costs time. Inspect the existing logging context's importance/lazy/resource-based path rather than inventing a new logging framework or an assumed `IsEnabled` API.

Diagnostic logging can be intentionally omitted or gated for cost/size; [binary-log event content](../../maintaining-binary-log-compatibility/references/event-content.md) describes the existing limits. Preserve the information required by the changed consumer.

Report the scenario, metric, baseline/candidate identity, effect, and uncertainty. Include relevant allocation/retention or startup effects when an optimization moves cost rather than removing it. Stop at the measured question; a source-derived recommendation should remain labeled as such.
