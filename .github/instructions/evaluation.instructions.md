---
applyTo: "src/Build/Evaluation/**"
---

# Evaluation

- Trace the relevant passes in [Evaluator](../../src/Build/Evaluation/Evaluator.cs). Property/import processing, item definitions, items, and targets do not share one universal "condition evaluated immediately" rule.
- Global properties normally override project assignments; account for `TreatAsLocalProperty`. Do not generalize last-write-wins to every property source.
- Preserve item/metadata scope, escaping, evaluation order, and lazy-item semantics. Undefined versus empty metadata must follow the specific API/operation contract.
- Expansion caches need the correct property/item/metadata context and lifetime. Repeating the same expression text does not imply the same result.
- Measure hot paths before optimizing. Avoid repeated allocations and filesystem work, but do not invent invocation counts or add caches without invalidation.
- For intrinsic/property functions, check the actual allowlist, feature guards, runtime availability, and existing error behavior. File/environment access is not uniformly an opt-in feature.
- Test the affected import, condition, global-property, and reevaluation scenarios. Assess observable changes with the [compatibility skill](../skills/assessing-breaking-changes/SKILL.md).

Load [performance guidance](../skills/optimizing-msbuild-performance/SKILL.md) for an actual performance change and [SDK integration](../skills/integrating-sdk-and-msbuild/SKILL.md) for SDK/import-boundary work.
