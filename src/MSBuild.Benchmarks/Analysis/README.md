# Evaluation cost analysis harness

Tooling for answering "where does MSBuild spend time during project evaluation, and what does each
part cost". The findings it produced are written up in
[`documentation/evaluation-cost-breakdown.md`](../../../documentation/evaluation-cost-breakdown.md).

Build the repository in Release first (`./build.cmd -configuration Release`); the harness locates the
bootstrap SDK under `artifacts/bin/bootstrap/core/sdk` so that SDK-style projects can be resolved.

## Analysis mode

```powershell
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --output breakdown.md
```

Creates and restores a `dotnet new console` fixture (pass `--project <path>` to use your own,
already restored, project) and emits a markdown report containing:

* an **inventory** of what the evaluation produced (imports, properties, items, targets, tasks),
  which gives the denominators for per-unit costs;
* **wall clock** for cold and warm evaluations at each `ProjectEvaluationStage`;
* a **phase breakdown** with inclusive and exclusive times, reconstructed from the `Microsoft-Build`
  event source markers, for both cold and warm regimes;
* **file system work**, logical versus real operations, through an injected `MSBuildFileSystemBase`;
* **allocations and GC pause** per evaluation;
* a decomposition of **XML document loading** into stat, read, tokenize, DOM, and location tracking;
* a **steady state check** that re-measures at the end of the run, so you can tell whether tiered
  JIT compilation was still in progress.

Useful options: `--iterations <n>` (default 11), `--msbuild-exe-path <path to MSBuild.dll>`.

## Multi-project mode

```powershell
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --multi-project <directory>
```

Evaluates every `.csproj` under a directory twice over: once in a single `ProjectCollection` with one
shared `EvaluationContext` (what a solution or graph build does) and once with a fresh collection and
context per project. Reports the first-project cost and the marginal cost of each later project, which
is what determines whether evaluation in a large build is dominated by warming the caches or by the
per-project work that follows.

## Benchmarks

```powershell
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --filter *FullEvaluationBenchmark*
```

`FullEvaluationBenchmark` measures full evaluation of the same real fixture, cold versus warm and
`Full` versus `Items` versus `Properties`, with `MemoryDiagnoser`. These are the statistically valid
numbers; the analysis mode is calibrated against them.

## CPU profile

```powershell
./Collect-EvaluationProfile.ps1 -Iterations 120
```

Runs the harness in `--profile-only` mode (warm up, then nothing but cold evaluations) under
`dotnet-trace`, then folds the resulting speedscope profile into cost categories with
`fold-evaluation-profile.py`. This is the lens that sees what the markers cannot: which syscall
inside `LoadDocument` is expensive, and how much of an evaluation is garbage collection.

Requires `dotnet tool install --global dotnet-trace` and Python 3.

## Notes

* The analysis targets .NET (Core) MSBuild; the `Analysis` folder is excluded from the .NET Framework
  build of this project.
* Absolute numbers are machine specific. Ratios and shares are what transfer between machines.
* Reaching steady state takes roughly 70 evaluations on a modern laptop. Any evaluation benchmark
  that warms up fewer times than that is measuring tiered JIT compilation, not evaluation.
