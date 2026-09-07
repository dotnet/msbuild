# MSBuild review lenses

These preserve the reviewer's domain coverage without requiring 24 separate passes or agents. Select lenses from the changed behavior and its consumers; a lens is a question to investigate, not a presumption that a violation exists.

| Lens | Concrete question |
|---|---|
| 1. Backward compatibility | Which previously working build, host, task, logger, or public API consumer changes behavior? Distinguish a regression from an intentional opt-in contract. |
| 2. ChangeWaves | Does the compatibility assessment call for a wave? If so, is the applicable wave used and are enabled/disabled paths covered? Do not require a wave for every internal fix. |
| 3. Performance and allocations | Is this actually a hot path? Can measurements attribute a regression to repeated I/O, reflection/type discovery, allocations, parsing, collection choice, or synchronization? Cache discovery only with the correct type/assembly lifetime. |
| 4. Coverage and completeness | Would the assertion fail without the fix? Do tests exercise the intended implementation, TFM, host, mode, and relevant negative case? |
| 5. Diagnostics | Are severity, resource formatting, codes, and user action correct? Could a new warning break `WarnAsError`? |
| 6. Logging and transport | Is each relevant event subscribed, forwarded, serialized, replayed, and filtered correctly at the actual boundary? Binlog and node IPC are separate protocols. |
| 7. String comparison | Are MSBuild names, paths, arbitrary text, and identifiers compared according to their contracts, with escaping/culture handled at the right layer? |
| 8. API surface | Is a public addition necessary and compatible across supported runtimes? Follow existing reference-assembly/package validation rather than inventing baseline files. |
| 9. Target authoring | Are import/evaluation timing, extensibility points, dependencies, conditions, and incremental outputs correct? `BeforeTargets`/`AfterTargets` are legitimate extension mechanisms. |
| 10. Architecture and design | Does the fix honor the original requirement and layer ownership? Are a workaround's claimed constraints real, and is an existing supported API sufficient? |
| 11. Platform and runtime | Which OS, architecture, filesystem, and TFM assumptions are observable? Test supported variants; do not demand identical platform-specific behavior. |
| 12. Simplification | Is there a smaller equally correct implementation using existing helpers? Do not expand the PR into unrelated cleanup. |
| 13. Concurrency and lifetime | Who owns each mutable value? Can a concrete interleaving violate the invariant? Check confinement, existing locks, cancellation, reentrancy, and three-or-more participants where relevant. |
| 14. Naming | Does a name imply incorrect semantics or conceal a material distinction? Defer cosmetic preferences unless style review was requested. |
| 15. SDK integration | Are CLI forwarding, restore/build evaluations, design-time builds, and the project-reference protocol preserved? Trace into the correct SDK/NuGet revision. |
| 16. C# and nullable contracts | Are features supported by the repository compiler/runtime combination? Does nullability reflect the actual API contract and surrounding file convention? |
| 17. File I/O and paths | Are relative roots, virtual task environments, UNC/long paths, symlinks, enumeration, and cache invalidation handled by the owning layer? |
| 18. Documentation | Is the wording true at the reviewed revision, in the authoring source, and appropriate for its audience? Preserve useful examples and distinguish implementation detail from public contract. |
| 19. Build infrastructure | Does the change affect bootstrap, Arcade, SDK, Visual Studio, packaging, or source-build entry points? Validate the ones actually affected. |
| 20. Scope and authority | Does the diff stay within the request? Record follow-ups without creating issues, resolving threads, or posting reviews unless authorized. |
| 21. Evaluation semantics | Does the change respect evaluation passes, global/local property rules, item/metadata scope, and import order? Do not assume every condition is evaluated immediately. |
| 22. Correctness and edge cases | What concrete input or sequence fails? Consider empty/invalid inputs and boundaries, but do not invent unsupported scenarios or paper over a structural bug. |
| 23. Dependencies | Is the dependency/feed/API compatible with all affected consumers and packaging rules? Verify current version and ownership rather than assuming a manual bump is always wrong. |
| 24. Security-relevant behavior | Does the change alter trust, loading, path handling, temporary files, or secret exposure? Keep ordinary review findings concrete; use the host's specialist workflow for an explicit security audit. |

## Evidence standard

For each suspected defect, record the preconditions, exact source location at head, failing sequence, consequence, and evidence. Check whether it already exists at the baseline. A lock, runtime guard, unsupported configuration, or unreachable path may disprove the suspicion.

For concurrency, write the relevant thread/process sequence rather than merely flagging a mutable field. Thread confinement and immutable snapshots can be valid alternatives to locks.

For a public API or build behavior change, name the consumer and the old/new observable behavior. A new interface method is not automatically safe because a runtime supports default interface implementations; MSBuild also supports older runtimes and third-party implementors.

For performance, separate measurement from interpretation. Run the [benchmark protocol](../../benchmarking-msbuild/SKILL.md) when the claim depends on wall-clock or allocation improvements.

## Completeness matrix

Use only for a requested completeness/proof review:

| Requirement or supported scenario | Implementation at head | Executed or source-derived evidence | Remaining gap |
|---|---|---|---|
| The specific behavior under review | Symbol and revision | Test/repro or trace, including baseline where relevant | Untested TFM/host/input, or none within this scenario |

Do not replace this matrix with "24/24 clean." Code correctness, available validation, and merge readiness are different conclusions.
