---
applyTo: "src/Build/Evaluation/**"
---

# Evaluation Engine Instructions

The evaluation engine (`Evaluator.cs`, `Expander.cs`, `LazyItemEvaluator.cs`) is MSBuild's hottest code path. Every project load passes through here.

## Evaluation Model Integrity

* Respect the strict evaluation order: environment → global properties → project properties (file order with imports) → item definitions → items. Do not introduce changes that alter this order.
* Conditions are evaluated at the point they appear, not deferred. Never move condition evaluation to a later phase.
* Undefined metadata and empty-string metadata must be treated equivalently — do not add logic that distinguishes them.
* Property precedence is last-write-wins within the import chain. Changes to import ordering (especially `Directory.Build.props` before SDK props) can silently break downstream consumers.
* Gate any evaluation behavior changes behind a ChangeWave — see [ChangeWaves docs](../../documentation/wiki/ChangeWaves.md).

## Expander Safety

* `Expander.cs` is called millions of times per evaluation. Every allocation counts.
* Use `ReadOnlySpan<char>` and `Slice()` to avoid string allocations in expansion.
* Cache expanded values when the same expression is expanded repeatedly.
* Use `MSBuildNameIgnoreCaseComparer` for property/item name lookups, not `StringComparer.OrdinalIgnoreCase`.

## IntrinsicFunctions

* New intrinsic functions are permanent public API — they can never be removed once shipped.
* Validate all inputs; intrinsic functions are called from user-authored MSBuild files with arbitrary arguments.
* Security-sensitive functions (file I/O, registry, environment) must check for opt-in before execution.
* Test edge cases: null arguments, empty strings, very long strings, culture-sensitive formatting.

## Performance (Critical Hot Path)

* `Evaluator.cs` (56 review comments) and `Expander.cs` (46 comments) are the two most-reviewed files in this folder.
* Avoid LINQ in any method called during evaluation — use `for`/`foreach` loops.
* Avoid `string.Format` on hot paths — use interpolation only when the result is needed.
* Choose collection types deliberately: `Dictionary` for lookup-heavy, `List` for iteration-heavy, consider `FrozenDictionary` for read-only after construction.
* Profile before optimizing — measure, do not guess.

## Condition Evaluation

* Condition parsing is allocation-sensitive. Prefer `Span<char>`-based parsing.
* Boolean conditions should short-circuit correctly.
* String comparisons in conditions use MSBuild semantics (case-insensitive for identifiers).

## ProjectRootElementCache

* Cache invalidation bugs cause stale evaluations or memory leaks. Test eviction scenarios.
* Thread safety is critical — the cache is accessed from multiple nodes.

## Related Documentation

* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [Target Maps](../../documentation/wiki/Target-Maps.md)
