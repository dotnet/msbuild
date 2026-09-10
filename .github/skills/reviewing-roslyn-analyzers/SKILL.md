---
name: reviewing-roslyn-analyzers
description: "Use when reviewing changes to a Roslyn C# DiagnosticAnalyzer, CodeFixProvider, DiagnosticSuppressor, diagnostic descriptor, AnalyzerReleases file, or analyzer tests. Also use for changes to diagnostic severity, configuration, or FixAll behavior."
argument-hint: "Paste the PR number/URL or the analyzer diff to review."
---

# Reviewing Roslyn Analyzer Contributions

A Roslyn analyzer runs in compiler hosts, concurrently and across compilations. Defects can cause false warnings, break `-warnaserror` builds, produce non-compiling fixes, or leave analysis incomplete after an exception.

This is the generic layer — the analyzer *engineering*. When the analyzer encodes MSBuild multithreaded-task rules (the `MSBuildTaskNNNN` diagnostics in `src/TaskAnalyzer/`), also apply **reviewing-multithreaded-task-analyzers**, which judges whether the encoded rule matches MT semantics.

## When to use

- A PR adds or modifies a `DiagnosticAnalyzer`, `CodeFixProvider`, `DiagnosticSuppressor`, `DiagnosticDescriptor`, or their tests.
- A PR changes a diagnostic's **severity**, **category**, **message**, or **default-enabled** state.
- A PR touches `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`, or should have.

Not for: MSBuild BuildCheck analyzers (`src/Build/BuildCheck/**` — different subsystem; see `buildcheck.instructions.md`), or reviewing task code being *migrated* to MT (use the `mt-migration-reviewer` agent).

## How to use

For an automated pass, invoke **`@analyzer-reviewer`**. For a manual review:

1. Identify changed rules, fixes, descriptors, configuration, and tests at the PR head. Mark each wave applicable or `N/A`.
2. Work through applicable Waves 1–5 in order. Verify each candidate against source or tests. Mark missing evidence `INCOMPLETE`.
3. Report coverage first, then proven findings with `file:line`, failing input, severity, and a fix. Include one row per wave.

Use only `CLEAN`, `ISSUES`, `N/A`, or `INCOMPLETE` for coverage status, never a finding's severity:

| Wave | Status | Verified / evidence |
|---|---|---|
| 1 Structure/release | CLEAN · ISSUES · N/A · INCOMPLETE | what you confirmed, a link to the finding, or why verification was incomplete |

**Evidence-and-noise gate:**
- CLEAN is the expected result; never invent a finding to fill a wave. INCOMPLETE is an honest verdict — it must never be upgraded to a finding.
- **No cap, no quota.** Report every concern that clears the evidence and severity bar, and only those.
- Report a finding only with a concrete trigger: an exact source/config input, a fix output that does not compile (name the `CSxxxx`), or an exact descriptor↔release↔README↔test mismatch.
- No praise, style, naming, formatting, preference-only, speculative-future ("a future caller might…"), or out-of-diff findings.

Severity: **BLOCKING** (build break, analyzer crash, non-compiling fix, false positive at Warning-or-higher), **MAJOR** (missed detection, theater test, release-tracking/severity drift), **MINOR** (a concrete message/config inaccuracy that does not change diagnostic behavior). No NIT.

---

## Wave 1 — Structure, descriptors & release tracking

The mechanical hygiene layer. Cheap to check, and the most frequent source of defects in practice.

**CHECK — flag if:**
- [ ] A new/changed rule is **not** reflected in `AnalyzerReleases.Unshipped.md` (columns `Rule ID | Category | Severity | Notes`). RS2000 requires it.
- [ ] The descriptor's `defaultSeverity` disagrees with the `AnalyzerReleases` row, the README rule table, or the test assertions. **These four must move together.** The project puts `RS2001` in `NoWarn`, so the build will *not* catch this drift — verify severities by hand.
- [ ] A new default-enabled Warning/Error, severity promotion, or newly enabled rule lacks justification for its `-warnaserror` impact.
- [ ] Diagnostic ID is not a compile-time constant, not unique, or not in `CATEGORYdddd` format (RS1017/1019/1018). Category is not the project's standard string.
- [ ] Title or message ends with a period, or description does not (RS1031/1032/1033).
- [ ] A rule reported from a `CompilationEnd`/`RegisterCompilationEndAction` path lacks `WellKnownDiagnosticTags.CompilationEnd` (RS1037).
- [ ] A reported descriptor is missing from `SupportedDiagnostics` (RS1005). (`SupportedDiagnostics` should return a cached static array — a perf convention, not an RS rule.)
- [ ] Localization/`helpLinkUri` is added to *some* descriptors but not others (RS1007/1015 are off by default — consistency, not mandate).

## Wave 2 — Correctness & robustness of the analyzer itself

An exception can leave analysis incomplete. This wave is BLOCKING-heavy.

**CHECK — flag if:**
- [ ] Any callback can **throw**. Roslyn normally reports analyzer failures as `AD0001`. Do not assume an exception removes diagnostics already reported or stops unrelated analyzers. Guard **locations** before reporting: a symbol may have zero `Locations`, and a metadata location has a null `SourceTree`. Never index `Locations[0]` or dereference `SourceTree` unconditionally. Use `Location.None` when no source location exists.
- [ ] Mutable state lives in an analyzer **instance field** (RS1008). Per-compilation state must be captured in closures inside `RegisterCompilationStartAction`. `Initialize` must call `EnableConcurrentExecution()` and `ConfigureGeneratedCodeAnalysis(...)` (RS1026/1025).
- [ ] `ISymbol` is compared with `==` instead of `SymbolEqualityComparer` (RS1024).
- [ ] Deduplication or suppression uses a key that does **not** match where the diagnostic is reported, so distinct violations collapse or suppression misfires. Report at the precise offending location, with secondary context as an `AdditionalLocation`, so `#pragma warning disable` / `[SuppressMessage]` work where the code lives.
- [ ] File/environment/other non-deterministic APIs are used inside analyzer callbacks (RS1035).

## Wave 3 — Detection precision (false positives & false negatives)

The heart of an analyzer review, and where reviewers spend the most words. An analyzer that cries wolf gets suppressed wholesale; one that misses the target gives false confidence.

**For every rule the PR adds or widens, construct inputs in each cell:**

| | Should fire | Should stay silent |
|---|---|---|
| **Direct** | the canonical violation | the canonical safe form |
| **Edge** | the tricky shape (nested call, array, named arg, alias, inherited member) | the recognized safe pattern the analyzer must not flag |

**CHECK — flag if:**
- [ ] A **safe pattern is flagged** (false positive). Trace the analyzer's own allow-list: does it recognize the argument alias, the local-variable initializer, the implicit conversion, the base-type member, the metadata/typed accessor? (E.g. an open-generic `ITaskItem<T>` — `TypeKind.TypeParameter` — must be skipped, because an unresolved type parameter can't be judged.)
- [ ] A **known violation is missed** (false negative) — e.g. the check keys on a syntactic shape (`==` on `.Kind`) rather than a semantic one, or on one argument position when others are also unsafe, or misses `using static`, nested calls, or members reached through a base class.
- [ ] Widening the rule's **scope/gate** silently changes which inputs fire — especially any change that makes an always-applicable rule newly scope-gated, or vice versa. Name who newly gains/loses a diagnostic.
- [ ] Parameter-name / metadata / attribute matching is case- or overload-sensitive in a way tests do not pin.

## Wave 4 — Code fixes

A code fix is applied unattended, in bulk, by `dotnet format` and IDE "Fix all". It must produce **compiling** code or offer **nothing**.

**CHECK — flag if:**
- [ ] Applying the fix can produce non-compiling code. Enumerate the contexts: nested calls (don't wrap the outer argument — CS1503), named-argument order, static context / static local / static lambda (CS0120), field/property/constructor initializers (CS0236/CS0027), a type with no accessible member the fix references (CS0103), init-only or non-public setters that can't satisfy an interface, a member-name collision (CS0102), a property referenced across partial declarations in another document. The correct behavior is to **withhold** the fix, not emit broken code.
- [ ] Every `CodeAction` does not set a stable, non-null `EquivalenceKey` (RS1010), or the provider does not override `GetFixAllProvider` (RS1016). Per the analyzer README, `dotnet format analyzers` derives its batch from the **first** diagnostic — if that first occurrence offers no fix, the whole batch applies nothing; the fixer must group diagnostics by target property so a property is retyped exactly once (`PreferTypedParameterFixAllProvider`).
- [ ] The fix changes program semantics (e.g. drops an overload parameter that alters behavior) rather than declining.

## Wave 5 — Tests

Analyzer tests are cheap to write and easy to fake. A test that cannot fail is worse than no test.

**CHECK — flag if:**
- [ ] A test **cannot fail for the reason it names** — e.g. it exercises a config path it never actually sets, or asserts "no diagnostic / no feedback" where the interesting case is a diagnostic.
- [ ] A new opt-in / config path added by the PR has no test that would fail if it silently became a no-op.
- [ ] Any rule lacks the coverage matrix: positive, negative, boundary, safe-pattern-suppresses, and (for a fix) fixed-code-**compiles**.
- [ ] Tests hard-code OS-specific absolute paths instead of the portable helper; or assert full localized strings instead of invariant substrings.

## Don't over-flag (avoid false alarms)

- Cold-path LINQ inside `RegisterCompilationStart` (once per compilation) is fine; only flag allocation/LINQ on the per-operation/per-symbol hot path.
- A rule that is `Info` or off-by-default is a deliberate, low-blast-radius choice — hold it to modernization-suggestion standards, not correctness-rule standards.
- A missing code fix is acceptable when the resolution is genuinely author-judgment (e.g. MSBuildTask0009 offers none by design). Don't demand a fix that would have to guess intent.
- `helpLinkUri`/localization absence is **not** a finding for an internal analyzer unless the PR introduces a concrete user-visible inconsistency (e.g. other descriptors carry help links and the new one doesn't).
