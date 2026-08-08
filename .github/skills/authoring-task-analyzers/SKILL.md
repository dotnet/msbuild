---
name: authoring-task-analyzers
description: 'Guides creation and modification of Roslyn analyzers, code fixes, and diagnostic suppressors for MSBuild task authors. Consult when adding MSBuildTaskxxxx or MSBuildTaskSPRxxxx rules, changing src/TaskAnalyzer, extending banned API detection, adding task-author migration guidance, or reviewing TaskAnalyzer behavior and coverage.'
argument-hint: 'Describe the task-author problem the analyzer should detect or fix.'
---

# Authoring Task Analyzers

`src/TaskAnalyzer` is a Roslyn analyzer package for people who implement MSBuild tasks. It is a separate product surface from engine and task diagnostics shown to users running MSBuild.

This guidance is derived from the PRs that established the current analyzer architecture:

- [#13444](https://github.com/dotnet/msbuild/pull/13444): MSBuildTask0001-0005 and task-author code fixes
- [#13926](https://github.com/dotnet/msbuild/pull/13926): required-property diagnostic suppressor
- [#13972](https://github.com/dotnet/msbuild/pull/13972): MSBuildTask0006-0008 and typed-parameter code fixes
- [#13973](https://github.com/dotnet/msbuild/pull/13973): MSBuildTask0009

Recurring review lessons from those PRs:

- **#13444:** classify severity by actual safety, support explicitly opted-in helper classes, and make analyzer state safe under concurrent callbacks.
- **#13926:** suppress only when MSBuild can satisfy the compiler contract; a get-only `[Required]` property is not assignable and must remain diagnosed.
- **#13972:** resolve conflicting inference before reporting, honor non-inherited opt-in attributes exactly, and withhold a code fix unless all affected references can be rewritten.
- **#13973:** mirror the engine's real binding support rather than an aspirational type list, and avoid repeating source-base diagnostics on every derived task.

## Choose the Correct Mechanism

Use a task analyzer when the person who must change code is the **task author**:

- The task implementation uses an unsafe process-wide API.
- A task property declaration cannot be bound correctly by MSBuild.
- The task could adopt a safer or more strongly typed API.
- A compiler diagnostic is incorrect because MSBuild initializes a task property later.

Use a normal `MSBxxxx` engine or task diagnostic when the person running the build must change a project, command line, SDK, toolset, or build input. See the [errors and warnings skill](../authoring-errors-and-warnings/SKILL.md).

Choose among the Roslyn mechanisms deliberately:

| Mechanism | Use when |
|---|---|
| `DiagnosticAnalyzer` | Task source contains a correctness problem or migration opportunity |
| `CodeFixProvider` | The change is mechanical and can be proven safe |
| `DiagnosticSuppressor` | A compiler or analyzer diagnostic is provably invalid under MSBuild's task contract |
| `BannedApiDefinitions` entry | An existing unsafe-API rule already has the correct scope, severity, and fix behavior |

## Required File Surfaces

For every new diagnostic:

1. Add a public ID constant to `src/TaskAnalyzer/DiagnosticIds.cs`.
2. Add a descriptor to `src/TaskAnalyzer/DiagnosticDescriptors.cs`.
3. Add the descriptor to `DiagnosticDescriptors.All`.
4. Add the rule to `src/TaskAnalyzer/AnalyzerReleases.Unshipped.md`.
5. Document its scope, rationale, examples, and safe alternatives in `src/TaskAnalyzer/README.md`.
6. Add analyzer tests in `src/TaskAnalyzer.Tests`.
7. Add performance scenarios in `src/TaskAnalyzer.Benchmarks/AnalyzerScenarios.cs`.

New analyzer and code-fix implementations belong in `src/TaskAnalyzer`. Add reusable metadata names to `WellKnownTypeNames.cs` and shared semantic helpers to `SharedAnalyzerHelpers.cs` instead of duplicating strings or logic.

Adding a rule to the existing projects does not require another `MSBuild.slnx` entry. New package dependencies must be centrally versioned and analyzer-only Roslyn dependencies must remain `PrivateAssets="all"`.

## IDs, Descriptors, and Severity

Analyzer IDs use `MSBuildTask####`. Suppression IDs use the separate `MSBuildTaskSPR####` namespace.

```csharp
public const string MyRule = "MSBuildTask0010";

public static readonly DiagnosticDescriptor MyRule = new(
    id: DiagnosticIds.MyRule,
    title: "Short action-oriented title",
    messageFormat: "Task property '{0}' has problem '{1}'",
    category: "MSBuild.TaskAuthoring",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Explain the problem and the safe task-authoring pattern.");
```

Use severity consistently with the existing rules:

- **Error**: the API is never safe in an MSBuild task and there is no valid interpretation.
- **Warning**: ignoring the diagnostic risks incorrect builds, shared-process corruption, or a runtime binding failure.
- **Info**: migration or design guidance where existing task code remains valid.

Warnings can break analyzer consumers that promote warnings to errors. Prefer `Info` for optional modernization and use `Warning` only for concrete correctness risks. Diagnostics reported from a compilation-end action must include `WellKnownDiagnosticTags.CompilationEnd`.

## Analyzer Implementation Pattern

Every analyzer must enable concurrency and skip generated code:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.MyRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }
}
```

Resolve metadata symbols once in the compilation-start action. If the mandatory `ITask` type is absent, return immediately. Treat every other metadata lookup as optional because the compilation may not reference that assembly or type.

Select the narrowest analysis scope:

- `RegisterSymbolAction` for declarations such as task property types.
- `RegisterSymbolStartAction` plus operation and symbol-end actions when analysis is scoped to a task type and requires deduplication.
- Compilation-wide operation collection plus `RegisterCompilationEndAction` only for genuinely transitive analysis such as a call graph.

Diagnostics intended to support code fixes should be reported from a local operation, symbol, or symbol-end context. Compilation-end diagnostics are non-local and are unsuitable for normal code-fix discovery.

Use `SymbolEqualityComparer.Default` for Roslyn symbols, including collection comparers. Match generic types through `OriginalDefinition`. Do not use reference equality or compare `ToDisplayString()` output.

Prefer standard BCL collections and straightforward control flow. Introduce a specialized collection only when a benchmark demonstrates a meaningful improvement that justifies the maintenance cost.

## Scope and False-Positive Control

False-positive avoidance is the primary design constraint. Before reporting:

- Prove the containing type is an `ITask` implementation or an explicitly supported helper type.
- Respect `msbuild_task_analyzer.scope` through `ReadAnalyzeAllTasksOption` when the rule participates in configurable scope.
- Match direct attributes when the attribute has `Inherited = false`; do not substitute interface inheritance.
- For task inputs, check accessibility, setter accessibility, `[Output]`, static members, arrays, and inherited properties as applicable.
- Avoid duplicate diagnostics for source-declared base properties. Report metadata-base problems on the derived source type only when that is the actionable location.
- Preserve path semantics across operating systems and use the existing path classifiers and safe-wrapper helpers.
- Return without a diagnostic whenever symbol resolution or data-flow evidence is incomplete.

When multiple observations infer one result, aggregate and resolve conflicts before reporting. For example, a property used as both a file and directory must not receive incompatible type suggestions.

## Code Fixes

Offer a code fix only when the rewrite is demonstrably safe:

- Check every reference that the fix must update. If any reference shape is unsupported, do not register the fix.
- Store machine-readable values such as property name and suggested type in `Diagnostic.Properties`; do not parse the diagnostic message.
- Reuse the same classifiers and supported-type tables in the analyzer and code fix.
- Use `WellKnownFixAllProviders.BatchFixer` only for independent edits. Use a custom `FixAllProvider` for coordinated multi-site or per-type rewrites.
- Preserve defaults, null guards, partial declarations, array shape, qualification, and trivia.
- Add explicit no-fix tests for ambiguous or unsafe cases.

## Diagnostic Suppressors

A suppressor needs stronger proof than an analyzer:

1. Filter to the exact compiler or analyzer diagnostic ID.
2. Resolve the affected symbol from primary and additional locations.
3. Verify every MSBuild-specific precondition.
4. Suppress only when no user action is required.

The required-property suppressor demonstrates the standard: the containing type implements `ITask`, the property has `Microsoft.Build.Framework.RequiredAttribute`, and the property has a public setter that MSBuild can populate. A similarly named attribute or a get-only property must not be suppressed.

## Tests

Use the infrastructure in `src/TaskAnalyzer.Tests/TestHelpers.cs`. Add a focused helper for a new analyzer when that keeps test setup explicit.

Each rule needs:

- A minimal positive case with exact ID, location, severity, and relevant message arguments.
- Negative cases for non-task types and out-of-scope task types.
- Missing-reference and unresolved-symbol cases.
- Inheritance, nested type, partial type, and duplicate-reporting cases when relevant.
- Accessibility, static, get-only, and `[Output]` cases for task properties.
- Cross-platform path cases for path-sensitive rules.
- Conflicting evidence and deduplication cases for inferred diagnostics.
- Code-fix output plus no-fix cases for unsupported references.
- Suppressor cases that inspect `Diagnostic.IsSuppressed` and near-miss cases that remain unsuppressed.

Keep `TestHelpers.FrameworkStubs` and `AnalyzerSourceFactory.FrameworkStubs` synchronized when a rule requires another MSBuild type.

## Performance Harness

Every new rule must extend `src/TaskAnalyzer.Benchmarks/AnalyzerScenarios.cs`. The harness is not merely a regression test: it provides the common workload needed to compare a new analyzer with the analyzers already shipped in the package. Benchmark mechanics belong in `AnalyzerBenchmarks.cs` and `AnalyzerBenchmarkInfrastructure.cs`; an ordinary new rule should not need to modify them.

1. Add each new `DiagnosticAnalyzer` class to `AnalyzerScenarios.Analyzers`. An analyzer class implementing several IDs needs one entry. This covers both the early-exit no-op benchmark and the compliant-task benchmark. For a suppressor, use the suppressed compiler diagnostic ID and set `includeCompilerDiagnostics: true`.
2. Extend `AnalyzerSourceFactory.CompliantTask` when necessary so the new analyzer reaches its normal analysis path without producing a diagnostic. Do not replace the early-exit benchmark; the two scenarios measure different fixed costs.
3. Add every diagnostic ID to `AnalyzerScenarios.DiagnosticScenarios`.
4. Add a source factory that produces exactly `count` independent diagnostics. The harness runs 1, 10, and 100 instances.
5. Keep setup validation exact; a benchmark that produces the wrong diagnostic count is invalid.
6. For suppressors, measure no-op cost, compliant-task cost, and 1, 10, and 100 suppressed diagnostics. Construct `AnalyzerRunner` with `includeCompilerDiagnostics: true`, validate the suppressed compiler diagnostic ID, and set `requireSuppressed: true`. Generalize `AnalyzerSuppressorBenchmark` if another suppressor is added.

Benchmark sources should isolate analyzer work, avoid unrelated compiler diagnostics, reuse the existing framework stubs, and run with production concurrent-analysis settings.

Use a short focused run while iterating. For reportable results, run the full harness so the new and existing analyzers are measured by the same process, runtime, SDK, and BenchmarkDotNet configuration:

```powershell
dotnet run -c Release --project src\TaskAnalyzer.Benchmarks\TaskAnalyzer.Benchmarks.csproj -- --filter "*MSBuildTask0010*" --job short
dotnet run -c Release --project src\TaskAnalyzer.Benchmarks\TaskAnalyzer.Benchmarks.csproj
```

Review execution time and managed allocations for all four dimensions:

- **Early-exit no-op cost:** compare initialization and fast-exit cost when required MSBuild symbols are unavailable.
- **Compliant-task cost:** compare the fixed cost of running the normal analysis path over valid task code that produces no diagnostic.
- **One-diagnostic cost:** compare the cost of doing useful analysis and reporting one diagnostic.
- **Scaling:** compare the change from 1 to 10 to 100 diagnostics. Consider both the absolute 100-diagnostic cost and whether time or allocations grow disproportionately.

Use the comparison to make an explicit optimization decision:

- Optimize when the new analyzer is a clear time, allocation, or scaling outlier relative to comparable existing analyzers. Profile the outlier, improve the identified hot path, and rerun the same comparison to demonstrate the effect.
- Do not optimize merely because the analyzer allocates or takes measurable time. If its results are broadly in line with comparable analyzers and scale similarly, keep the straightforward implementation and avoid speculative complexity.

Include the comparison and the optimize-or-stop decision in the pull request. Do not compare absolute results produced by different runtime versions or materially different machines; rerun the relevant existing analyzers alongside the new analyzer instead.

## Completion Checklist

- [ ] Audience is a task author, not a build end user
- [ ] ID, descriptor, `All`, release manifest, and README updated
- [ ] Severity reflects correctness risk rather than desired visibility
- [ ] Analyzer enables concurrency and excludes generated code
- [ ] Metadata lookups are cached, nullable, and compared with `SymbolEqualityComparer.Default`
- [ ] Scope and false-positive cases are explicitly handled
- [ ] Code fix is withheld unless every required rewrite is safe
- [ ] Positive, negative, inheritance, deduplication, and cross-platform tests added as applicable
- [ ] Benchmark no-op scenario added for a new analyzer class
- [ ] Compliant-task benchmark reaches the normal analysis path and produces zero diagnostics
- [ ] Exact-count 1/10/100 benchmark scenario added for every diagnostic
- [ ] Suppressor benchmarks added when applicable
- [ ] New results compared with existing analyzers under the same benchmark environment
- [ ] Benchmark evidence and the resulting optimize-or-stop decision documented in the pull request
