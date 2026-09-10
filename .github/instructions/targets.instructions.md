---
applyTo: "src/Tasks/**/*.targets,src/Tasks/**/*.props"
---

# Targets and props

- Preserve target names and extensibility contracts. Use `DependsOnTargets` for required dependencies and `BeforeTargets`/`AfterTargets` for the appropriate extension relationship; the latter are not inherently inferior.
- Distinguish evaluation-time property/item groups from those inside executing targets. A target condition is not evaluated merely when its XML is first encountered.
- Use conditional property defaults when user values should win. Some targets deliberately compute/override internal properties; preserve their actual contract rather than prohibiting every assignment after imports.
- Quote string values in conditions and follow existing escaping/comparison conventions.
- Define `Inputs`/`Outputs` when target-level incremental skipping is appropriate. Include the dependencies and outputs needed for correctness, and account for output inference, batching, removed inputs, and clean behavior.
- Test relevant no-op, source-change, project-reference, multitargeting, and import-order cases. A successful clean build alone does not establish incremental correctness.
- Use the [SDK integration skill](../skills/integrating-sdk-and-msbuild/SKILL.md) for SDK imports, restore/build, or project-reference protocol changes.
- Assess changed defaults, item sets, ordering, or output with the [compatibility skill](../skills/assessing-breaking-changes/SKILL.md); do not assume every targets edit needs a ChangeWave.

Read [target maps](../../documentation/wiki/Target-Maps.md) or [unexpected rebuilding](../../documentation/wiki/Rebuilding-when-nothing-changed.md) only for the behavior under investigation.
