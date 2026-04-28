---
applyTo: "src/Build/Globbing/**"
---

# Globbing Instructions

Resolves file patterns (e.g., `**/*.cs`) for item includes/excludes. Runs during evaluation — performance-critical.

## Glob Pattern Correctness

* Semantics: `*` matches within a directory, `**` across directories, `?` a single character.
* Include and exclude patterns must use the same matching rules.
* Handle edge cases: empty patterns, only-wildcard patterns, literal special characters, trailing separators.
* Relative vs absolute patterns must produce consistent results regardless of working directory.

## Performance

* Minimize filesystem enumeration — prune directory traversal early using glob structure.
* Cache results when the same pattern is evaluated multiple times (common with `**/*.cs` across imports).
* Avoid allocating intermediate string collections — use lazy evaluation where possible.

## Exclude Patterns

* Excludes are applied after includes, not during.
* `Remove` with globs must use the same matching engine.
* Test with nested excludes (e.g., `**/*.cs` include with `**/obj/**` exclude).

## Evaluation-Time Behavior

* Globs resolve at evaluation time — filesystem state at that point determines the item list.
* Changes affect all SDK-style projects (implicit `**/*.cs` includes).
* Gate behavioral changes behind a [ChangeWave](../../documentation/wiki/ChangeWaves.md).

## Cross-Platform

* Glob patterns must work with both `\` and `/` separators.
* Case sensitivity must follow OS filesystem conventions.
* Symlink traversal must be safe (no infinite loops).
