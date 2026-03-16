---
name: reviewing-msbuild-code
description: "Reviews MSBuild code changes using @rainersigwald's 24-dimension methodology distilled from 10,081 comments (2016–2026). Activates for code review, PR review, pull request analysis, design review, architecture review, code quality assessment, or style check of MSBuild code. Covers backwards compatibility, ChangeWave discipline, performance, allocation awareness, test coverage, error message quality, logging, string comparison, API surface, target authoring, cross-platform correctness, code simplification, concurrency, naming, SDK integration, evaluation model integrity, correctness, dependency management, security, and build infrastructure."
---

# Rainer Sigwald — MSBuild Review Methodology

Review methodology distilled from **10,081 review comments** by **@rainersigwald** (2016–2026) across `dotnet/msbuild`, `dotnet/sdk`, and `dotnet/project-system`.

> When review eras conflict, 2024–2026 takes precedence. Core concerns — backwards compatibility, test coverage, performance — are consistent across all eras.

For the full reviewer persona, use `@rainersigwald-reviewer`. For detailed per-dimension checklists, see [DIMENSIONS.md](DIMENSIONS.md).

---

## Overarching Principles

1. **Backward Compatibility Is the Default** — New warnings are breaking changes for `WarnAsError` users. Behavioral changes require ChangeWave opt-in.
2. **Explicit Over Implicit** — Build behavior must be predictable and traceable.
3. **Performance Is Architectural** — Allocation patterns and collection choices are design decisions. MSBuild evaluates thousands of projects.
4. **Stability Over Velocity** — Changes must be incremental, well-tested, and reversible.
5. **Simplicity Wins** — Fewer lines, clearer control flow, less abstraction.
6. **Every Change Needs a Test** — Bug fixes need regression tests. Features need scenario tests.
7. **Error Messages Are UI** — Must be actionable with `MSBxxxx` codes.
8. **Binary Log Is Truth** — Must capture all meaningful events. Format changes must be backward-compatible.
9. **Consider the Ecosystem** — .NET SDK, Visual Studio, NuGet, Roslyn, runtime impacts.
10. **Opt-In Stages** — Design doc → preview → default → legacy support. No big-bang changes.
11. **Document the Why** — Rationale in comments and commit messages.
12. **Minimize Public API** — Default to `internal`. Every public member is a long-term commitment.
13. **Scope Discipline** — Single concern per PR. Track follow-ups as issues.

---

## Review Dimensions

Apply **all** dimensions on every review, weighted by file location (see [Folder Hotspot Mapping](#folder-hotspot-mapping)). Each dimension's rules are summarized below; for full checklists see [DIMENSIONS.md](DIMENSIONS.md).

### BLOCKING Severity

#### 1. Backwards Compatibility Vigilance (387 comments)
Gate breaking changes behind ChangeWave. New warnings break `WarnAsError` builds. Make behavioral changes opt-in. Never remove CLI switches — deprecate first. See `../../../documentation/wiki/ChangeWaves.md`.

#### 2. ChangeWave Discipline (71 comments)
Use correct version (next release, not current). Test both enabled and disabled paths. Document in ChangeWaves tracking file. See `../../../documentation/wiki/ChangeWaves-Dev.md`.

#### 13. Concurrency & Thread Safety (79 comments)
Shared mutable state must be thread-safe. Handle in-proc vs out-of-proc differences. Consider IPC ordering and reentrancy. See `../../../documentation/wiki/Nodes-Orchestration.md`.

#### 21. Evaluation Model Integrity (112 comments)
Respect evaluation order: env → global props → project props → item defs → items. `Directory.Build.props` before SDK props. Undefined metadata = empty string. See `../../../documentation/High-level-overview.md`.

#### 24. Security Awareness (22 comments)
Security-relaxing params require explicit opt-in. Task loading unifies to running MSBuild assemblies. Guard against path traversal and symlink attacks.

### MAJOR Severity

#### 3. Performance & Allocation Awareness (348 comments)
Minimize allocations on hot paths (`Evaluator.cs`, `Expander.cs`). Avoid LINQ in tight loops. Cache computed values. Choose collections for access pattern. Profile before optimizing.

#### 4. Test Coverage & Completeness (916 comments — most frequent)
All changes need tests. Bug fixes need regression tests. Descriptive test names (`Method_Scenario_Expected`). Deterministic tests. Cover edge cases and error paths.

#### 5. Error Message Quality (148 comments)
Actionable messages with `MSBxxxx` codes. Use `ResourceUtilities.FormatResourceStringStripCodeAndKeyword`. Correct severity. All user-facing strings in `.resx`. See `../../../documentation/assigning-msb-error-code.md`.

#### 7. String Comparison Correctness (73 comments)
`MSBuildNameIgnoreCaseComparer` for names. `OrdinalIgnoreCase` for identifiers — never `CurrentCulture`. OS-appropriate file path comparison. `DateTime` fields suffixed with `Utc`.

#### 8. API Surface Discipline (156 comments)
Default to `internal`. Record public API in `PublicAPI.Unshipped.txt`. Never remove — deprecate with `[Obsolete]`. XML doc on all public members. See `../../../documentation/wiki/Microsoft.Build.Framework.md`.

#### 9. Target Authoring Conventions (103 comments)
`DependsOnTargets` over `BeforeTargets`/`AfterTargets`. Proper conditions at correct evaluation point. Incremental builds need `Inputs`/`Outputs`. Respect SDK import ordering.

#### 10. Design Before Implementation (243 comments)
Discuss tradeoffs first. Complex features need specs (see `../../../documentation/specs/`). Incremental commits. Follow established patterns.

#### 11. Cross-Platform Correctness (99 comments)
Cross-platform APIs only. Handle Framework vs Core differences. UNC paths, long paths, symlinks. Build output must not differ by OS.

#### 15. SDK Integration Boundaries (605 comments)
Respect evaluation/execution separation. Property defaults must not override user values. Cross-stack coordination for protocol changes. Restore ≠ Build. See `../../../documentation/ProjectReference-Protocol.md`.

#### 17. File I/O & Path Handling (84 comments)
Use `FileUtilities` helpers. Handle UNC and long paths. Globbing must handle excludes. See `../../../documentation/WhenGlobbingReturnsOriginalFilespec.md`.

#### 19. Build Infrastructure Care (183 comments)
Pin versions via Darc/Maestro. Verify all build entry points. Validate CI changes before merge. See `../../../documentation/wiki/Bootstrap.md`.

#### 22. Correctness & Edge Cases (471 comments)
Verify edge cases (null, empty, Unicode, large inputs). Match documented semantics. Validate fixes against repros. Fail fast with clear errors.

### MODERATE Severity

#### 6. Logging & Diagnostics Rigor (195 comments)
Changes must appear in binary log. Correct `MessageImportance`. Diagnostic logging on complex paths. See `../../../documentation/wiki/Binary-Log.md`.

#### 12. Code Simplification (234 comments)
Flatten nesting with guard clauses. Use existing shared utilities. Remove dead code. Prefer `switch` expressions and pattern matching.

#### 18. Documentation Accuracy (384 comments)
Explain _why_ in comments. XML docs on public/complex code. `learn.microsoft.com` URLs. Specs need problem statements, non-goals, examples.

#### 20. Scope & PR Discipline (178 comments)
Track follow-ups as issues. Don't mix refactoring with behavior changes. Cross-reference related issues. Resolve feedback before merge.

#### 23. Dependency Management (111 comments)
Minimize references. Use Darc/Maestro. Binding redirect changes need impact analysis.

### NIT Severity

#### 14. Naming Precision (181 comments)
Descriptive names revealing intent. Consistent with surrounding code. Test methods: `Method_Scenario_Expected`.

#### 16. Idiomatic C# Patterns (297 comments)
Modern C# features. Match codebase conventions. Explicit nullability. Track framework constraints.

---

## MSBuild Knowledge Areas

| # | Area | Key Rules | Docs |
|---|------|-----------|------|
| 1 | **Name Comparisons** | `MSBuildNameIgnoreCaseComparer` for names. `OrdinalIgnoreCase` for identifiers. | — |
| 2 | **ChangeWave** | Correct version. Test opt-out. Document. | `../../../documentation/wiki/ChangeWaves.md` |
| 3 | **Breaking Changes** | New warnings break `WarnAsError`. Never remove CLI switches. | — |
| 4 | **Evaluation Order** | Last-write wins. `Directory.Build.props` before SDK props. | `../../../documentation/High-level-overview.md` |
| 5 | **Target Ordering** | `DependsOnTargets`. `Inputs`/`Outputs` for incremental. | `../../../documentation/wiki/Target-Maps.md` |
| 6 | **Binary Log** | All events captured. Format backward-compatible. | `../../../documentation/wiki/Binary-Log.md` |
| 7 | **Node Architecture** | In-proc and out-of-proc. Thread-safe shared state. | `../../../documentation/wiki/Nodes-Orchestration.md` |
| 8 | **Error Codes** | `MSBxxxx`. Unique. `ResourceUtilities`. Actionable. | `../../../documentation/assigning-msb-error-code.md` |
| 9 | **SDK Boundary** | Props before user code. Defaults don't override. Restore ≠ Build. | `../../../documentation/ProjectReference-Protocol.md` |
| 10 | **VS Servicing** | VS version branches. Side-by-side compatibility. | — |
| 11 | **Resource Strings** | Embedded codes. `.resx` for user-facing. Changes semi-breaking. | — |
| 12 | **Task Loading** | Unify to running assemblies. Framework vs Core differences. | `../../../documentation/wiki/Tasks.md` |

---

## Folder Hotspot Mapping

| Folder | Priority Dimensions | Hot Files |
|--------|---------------------|-----------|
| `src/Build/BackEnd/` | Concurrency, Performance, Correctness, Logging | `BuildManager.cs`, `NodeProviderOutOfProcBase.cs` |
| `src/Build/Evaluation/` | Evaluation Model, Performance, String Comparison, Compat | `Evaluator.cs`, `Expander.cs`, `IntrinsicFunctions.cs` |
| `src/Build/Logging/` | Logging, Design, API Surface | `ParallelConsoleLogger.cs`, `BuildEventArgsWriter.cs` |
| `src/Build/Construction/` | API Surface, Compat, Evaluation Model | `SolutionFile.cs`, `ProjectRootElement.cs` |
| `src/Build/Graph/` | Correctness, Performance, Simplification | `ProjectGraph.cs`, `GraphBuilder.cs` |
| `src/Build/Instance/` | API Surface, Performance, Correctness | `TaskRegistry.cs`, `ProjectInstance.cs` |
| `src/Build/Resources/` | Error Messages, Compat, ChangeWave | `Strings.resx` |
| `src/Tasks/` | Target Conventions, Cross-Platform, Compat | — |
| `src/Shared/` | Performance, Cross-Platform, File I/O | — |
| `src/MSBuild/` | Error Messages, Compat, Logging | — |
| `src/Framework/` | API Surface, Compat, Cross-Platform | — |
| `documentation/` | Documentation, Design | specs |
| `eng/` | Build Infrastructure, Dependencies | `Versions.props` |

---

## Review Workflow

1. **Map changed files** to folders in the hotspot table.
2. **Apply ALL 24 dimensions**, weighted by folder priority.
3. **Check backwards compatibility first** — most common blocker.
4. **Categorize findings:**
   - **BLOCKING**: Compat violations, ChangeWave omissions, concurrency bugs, security, evaluation model.
   - **MAJOR**: Missing tests, perf regressions, bad errors, API issues, correctness.
   - **MODERATE**: Logging gaps, docs, infrastructure, scope.
   - **NIT**: Naming, idioms, simplification.
5. **Ask probing questions** — "What happens when X is null?", "Has this been profiled?"
6. **Reference documentation** — link to `documentation/wiki/` and `documentation/specs/`.
7. **Track follow-ups** — suggest issues for non-blocking concerns.
