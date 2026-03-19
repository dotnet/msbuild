---
applyTo: "src/Build/Evaluation/**"
---

# Evaluation Engine Instructions

The evaluation engine (`Evaluator.cs`, `Expander.cs`, `LazyItemEvaluator.cs`) is MSBuild's hottest code path. Every project load passes through here.

## Evaluation Model Integrity

* Strict evaluation order: environment → global properties → project properties (file order with imports) → item definitions → items. Never alter this order.
* Conditions are evaluated at the point they appear, not deferred.
* Undefined metadata and empty-string metadata must be treated equivalently.
* Property precedence is last-write-wins within the import chain.
* Gate evaluation behavior changes behind a [ChangeWave](../../documentation/wiki/ChangeWaves.md).

## Expander Safety

* `Expander.cs` is called millions of times per evaluation — every allocation counts.
* Cache expanded values when the same expression is expanded repeatedly.

## IntrinsicFunctions

* New intrinsic functions are permanent public API — can never be removed once shipped.
* Validate all inputs; called from user-authored MSBuild with arbitrary arguments.
* Security-sensitive functions (file I/O, registry, environment) must check for opt-in.
* Test edge cases: null, empty strings, very long strings, culture-sensitive formatting.

## Condition Evaluation

* Condition parsing is allocation-sensitive — prefer `Span<char>`-based parsing.
* Boolean conditions must short-circuit correctly.
* String comparisons in conditions use MSBuild semantics (case-insensitive for identifiers).

## ProjectRootElementCache

* Cache invalidation bugs cause stale evaluations or memory leaks — test eviction scenarios.
* Thread safety is critical — the cache is accessed from multiple nodes.

## Related Documentation

* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [Target Maps](../../documentation/wiki/Target-Maps.md)
