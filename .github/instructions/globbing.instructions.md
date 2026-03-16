---
applyTo: "src/Build/Globbing/**"
---

# Globbing Instructions

The globbing engine resolves file patterns (e.g., `**/*.cs`) used in item includes and excludes. It runs during evaluation and is performance-critical.

## Glob Pattern Correctness

* Glob semantics must match MSBuild's documented behavior — `*` matches within a directory, `**` matches across directories, `?` matches a single character.
* Ensure consistency between include and exclude pattern evaluation — excludes must use the same matching rules as includes.
* Handle edge cases: empty patterns, patterns with only wildcards, patterns with literal special characters, trailing separators.
* Relative vs absolute path patterns must produce consistent results regardless of working directory.

## Performance

* Globbing runs during evaluation for every project — it is a hot path.
* Minimize filesystem enumeration: use the glob structure to prune directory traversal early.
* Cache glob results when the same pattern is evaluated multiple times (common with `**/*.cs` across imports).
* Avoid allocating intermediate string collections for directory enumeration — use lazy evaluation where possible.

## Exclude Patterns

* Exclude pattern handling is a common source of bugs. Ensure:
  - Excludes are applied after includes, not during.
  - Exclude patterns support the same wildcard syntax as includes.
  - Removing items via `Remove` with globs uses the same matching engine.
* Test with nested excludes (e.g., `**/*.cs` include with `**/obj/**` exclude).

## Evaluation-Time Behavior

* Globs are evaluated at evaluation time, not execution time. The filesystem state at evaluation time determines the item list.
* Changes to globbing behavior affect all SDK-style projects (which use implicit `**/*.cs` includes).
* Any behavioral change must be gated behind a ChangeWave — see [ChangeWaves](../../documentation/wiki/ChangeWaves.md).

## Cross-Platform Considerations

* Path separators in glob patterns must work on both Windows (`\`) and Unix (`/`).
* Case sensitivity of glob matching must follow the OS filesystem conventions.
* Symlink handling during directory traversal must be consistent and safe (no infinite loops).
