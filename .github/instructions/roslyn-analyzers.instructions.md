---
applyTo: "src/TaskAnalyzer/**/*.cs,src/TaskAnalyzer.Tests/**/*.cs"
---

# Roslyn Analyzer Instructions

The `src/TaskAnalyzer/` project (`Microsoft.Build.TaskAuthoring.Analyzer`) is a **Roslyn C# analyzer** — `DiagnosticAnalyzer` + `CodeFixProvider` types built on `Microsoft.CodeAnalysis.Diagnostics`. It ships diagnostics `MSBuildTaskNNNN` (category `MSBuild.TaskAuthoring`) that steer task authors toward thread-safe patterns.

These rules are for authoring and reviewing **the analyzer itself**. They are generic Roslyn-analyzer engineering conventions; the multithreading *semantics* the rules encode live in [multithreaded-task-analyzer.instructions.md](./multithreaded-task-analyzer.instructions.md). MSBuild's own build analyzers (BuildCheck) are a different subsystem — see [buildcheck.instructions.md](./buildcheck.instructions.md).

## Analyzer Shape & Registration

* Each analyzer is `[DiagnosticAnalyzer(LanguageNames.CSharp)]`, `sealed`, and stateless. `Initialize` calls `EnableConcurrentExecution()` and `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` (RS1026/RS1025).
* Resolve well-known types (`ITask`, `TaskEnvironment`, `AbsolutePath`, …) and build the banned-API lookup **once** per compilation inside `RegisterCompilationStartAction`; close over them. Never cache symbols in instance fields (RS1008).
* Scope work per type with `RegisterSymbolStartAction`/`RegisterSymbolAction`, then `RegisterOperationAction` for per-call checks. Prefer operation/symbol actions over `RegisterSyntaxNodeAction` — they are semantic and language-agnostic.
* Keep `AnalyzeXxx` methods `private static` — no captured instance state. Compare symbols with `SymbolEqualityComparer.Default`, never `==` (RS1024).
* Resolve banned APIs via `DocumentationCommentId` into a `Dictionary<ISymbol, …>` for O(1) lookup. No LINQ or repeated symbol resolution on the per-operation hot path.

## Diagnostics & Descriptors

* IDs are constants in `DiagnosticIds`; descriptors live in `DiagnosticDescriptors.All` in declaration order. Category is always `MSBuild.TaskAuthoring`.
* Keep **descriptor severity, `AnalyzerReleases.Unshipped.md`, the README rule table, and the test assertions in agreement.** Drift between these four is the single most common defect in this project.
* Titles and messages end **without** a period; descriptions end **with** one (RS1031/1032/1033).
* A descriptor reported from a `CompilationEnd` action must carry `WellKnownDiagnosticTags.CompilationEnd` (RS1037) — e.g. MSBuildTask0005.
* `helpLinkUri` and `LocalizableResourceString`/`.resx` are not currently used (RS1015/RS1007 are off by default). If you add localization or help links, do it consistently across all descriptors.

## Analyzer Release Tracking (RS2000/RS2001)

* Every new rule — and every severity or category change — **must** update `AnalyzerReleases.Unshipped.md` (columns `Rule ID | Category | Severity | Notes`). A missing entry fails RS2000. New rules go in `Unshipped.md`; don't edit `Shipped.md` unless cutting a formal release.
* `TaskAnalyzer.csproj` sets `NoWarn=…;RS1038;RS2001` (RS1038 is expected — the analyzer references `Workspaces` for code fixes; don't "fix" it). Suppressing `RS2001` means the build will **not** catch a descriptor severity that disagrees with the release file — verify severities by hand, or temporarily un-suppress `RS2001`, whenever a rule's severity changes.

## Severity & Configuration

* A new `Warning`-by-default rule, or a `Warning→Error` promotion, is a **breaking change** for consumers building with `-warnaserror`. Call it out explicitly; prefer `Info` for modernization suggestions. (As reference points at time of writing, MSBuildTask0001 was the sole `Error` and MSBuildTask0013 shipped disabled-by-default — verify the current descriptors rather than trusting these counts.)
* Analysis scope is read **once** per compilation from the `msbuild_task_analyzer.scope` option (`all` | `multithreadable_only`). Consume it through a `CompilerVisibleProperty`/`build_property.*` that is actually reachable — a raw dotted `build_property` name that no `.props` exposes is a dead option.
* Per-rule severity is user-configurable through `.editorconfig`/`.globalconfig` (`dotnet_diagnostic.<id>.severity`). Do not invent bespoke scope/config knobs when severity configuration already expresses the intent. Note that `dotnet_diagnostic.*.severity` reaches the analyzer via `SyntaxTreeOptionsProvider`, not `AnalyzerConfigOptions`.

## Code Fixes

* Every `CodeAction` needs a stable, non-null `EquivalenceKey` (RS1010) and the provider overrides `GetFixAllProvider` (RS1016). Per the analyzer README, `dotnet format analyzers` derives its FixAll batch from the **first** diagnostic — if the first occurrence offers no fix, the whole batch applies nothing.
* A fix must produce **compiling** code or offer nothing. Withhold the fix where `TaskEnvironment` cannot bind: static context (CS0120), field/property/constructor initializers (CS0236/CS0027), or a task with no `TaskEnvironment` member (CS0103). Handle nested calls, named-argument order, init-only/private setters, member-name collisions (CS0102), and properties referenced across partial declarations in another document (decline rather than break).

## Robustness

* An analyzer must **never throw** — an exception suppresses *all* diagnostics for the compilation. Guard **locations** before reporting: a symbol may have zero `Locations`, and a `Location` from metadata has a null `SourceTree` — never index `Locations[0]` or dereference `SourceTree` unconditionally; use `Location.None` as the fallback.
* Do not use file/environment/other non-deterministic APIs inside analyzer callbacks (RS1035).
* Deduplicate diagnostics on the exact key you report at; report at the precise offending location (with the task entry point as an `AdditionalLocation`) so `#pragma warning disable`/`[SuppressMessage]` work where the code lives.

## Tests (`src/TaskAnalyzer.Tests`)

* xUnit + Shouldly. Analyzer tests use the custom `TestHelpers` harness (`compilation.WithAnalyzers(...).GetAnalyzerDiagnosticsAsync()`); code-fix tests use `CSharpCodeFixTest<>`. Inject scope via `TestAnalyzerConfigOptionsProvider` with `build_property.msbuild_task_analyzer.scope`.
* Each rule needs: **positive** (fires), **negative** (silent), **boundary**, **safe-pattern-suppresses**, and — for fixes — **fixed-code-compiles**. Use `TestHelpers.FullyQualifiedPath` for OS-portable absolute paths.
* A test must be able to fail for the reason it names (an MT-scope test that sets no scope, or that asserts "no feedback", is theater).

## Related Documentation

* [Rule catalog & rationale](../../src/TaskAnalyzer/README.md)
* [Reviewing Roslyn analyzer contributions (skill)](../skills/reviewing-roslyn-analyzers/SKILL.md)
* [MT semantics the rules encode](./multithreaded-task-analyzer.instructions.md)
* [BuildCheck analyzers (different subsystem)](./buildcheck.instructions.md)
