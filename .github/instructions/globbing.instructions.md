---
applyTo: "src/Build/Globbing/**,src/Framework/Utilities/FileMatcher.cs"
---

# Globbing

- Preserve the established wildcard, escaping, include/exclude/remove, and relative-root semantics. Do not replace them with general-purpose filesystem glob assumptions.
- [FileMatcher](../../src/Framework/Utilities/FileMatcher.cs) is the implementation boundary for filesystem matching; it is not in `src/Shared`.
- Excludes can prune recursion during enumeration. Preserve that optimization and its equivalence to the intended result; do not force all excludes into a post-processing pass.
- Distinguish evaluation-time globs from item operations inside executing targets. Filesystem state and expansion context belong to the actual operation.
- Cache results only with a defined root, matching options, filesystem-state assumptions, and invalidation lifetime.
- Test relevant literal/wildcard patterns, nested excludes, separator/case behavior, symlinks, and missing paths. Assert MSBuild's established behavior rather than assuming OS filesystem rules alone define it.
- Measure enumeration and allocation costs before changing algorithms. Use [compatibility assessment](../skills/assessing-breaking-changes/SKILL.md) for observable matching changes.

Start with [FileMatcher tests](../../src/Framework.UnitTests/FileMatcher_Tests.cs) and the tests nearest the changed matching path; expand only for additional affected semantics.
