# MSBuild Benchmarks

This project contains performance benchmarks for MSBuild using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run All Benchmarks

```
cd src/MSBuild.Benchmarks
dotnet run -c Release
```

### Run Benchmarks on a Specific TFM

```
cd src/MSBuild.Benchmarks
dotnet run -c Release -f net472
dotnet run -c Release -f net11.0
```

### Filter to a Specific Benchmark Class

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*"
```

### Filter to a Single Benchmark Method

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark.IncludeOnly"
```

### Run the Orchard Core Evaluation Benchmarks

Restore an Orchard Core checkout, then pass the path to
`src/OrchardCore/OrchardCore/OrchardCore.csproj`. The benchmark intentionally does not set
`TargetFramework`, so it measures the outer cross-targeting evaluation used by the equivalent
CLI query. It runs only on the .NET Core target and is excluded from ordinary all-benchmark runs
unless the project path is set.

```powershell
dotnet run -c Release -f net11.0 -- --filter "*OrchardCoreEvaluationBenchmark*" --orchard-core-project "C:\src\OrchardCore\src\OrchardCore\OrchardCore\OrchardCore.csproj"
```

```bash
dotnet run -c Release -f net11.0 -- --filter "*OrchardCoreEvaluationBenchmark*" --orchard-core-project "$HOME/src/OrchardCore/src/OrchardCore/OrchardCore/OrchardCore.csproj"
```

Each measured method creates a fresh `ProjectCollection`, evaluates the project, and reads
`PackageReference` items or the `TargetFrameworks` property 100 times. The full-evaluation methods
model behavior before partial evaluation; the partial-evaluation methods model the optimized
`-getProperty` and `-getItem` paths. BenchmarkDotNet reports the normalized cost of one evaluation
and the ratio against full evaluation.

The project path can alternatively be provided through the
`MSBUILD_BENCHMARK_ORCHARDCORE_PROJECT` environment variable.

## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling
- `--orchard-core-project <path>` - Project used by `OrchardCoreEvaluationBenchmark`

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*" --job short --disable-ngen
```
