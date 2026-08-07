# MSBuild Benchmarks

This project contains performance benchmarks for MSBuild using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run Benchmarks Across Supported TFMs

On Windows, `Run-Benchmarks.ps1` runs each selected benchmark on both `net472` and `net11.0`.
Artifacts are kept separate under `artifacts\BenchmarkDotNet\<TFM>`.

```powershell
cd src/MSBuild.Benchmarks
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*"
```

Use `-Set` to run named benchmark sets without writing filter patterns:

```powershell
.\Run-Benchmarks.ps1 -Set Expansion
.\Run-Benchmarks.ps1 -Set PropertyExpansion
.\Run-Benchmarks.ps1 -Set PropertyFunctions
```

Multiple sets are combined with OR. Some sets are umbrellas for narrower sets:

| Umbrella set | Included sets |
| --- | --- |
| `Expansion` | `PropertyExpansion`, `PropertyExpansionScaling`, `PropertyFunctions`, `ItemExpansion`, `ItemFunctions`, `MetadataExpansion`, `MetadataExpansionScaling`, `MixedExpansion` |
| `PropertyExpansion` | Regular property expansion and `PropertyFunctions` |
| `ItemExpansion` | Regular item expansion and `ItemFunctions` |
| `Conditions` | `ConditionParsing`, `ConditionEvaluation` |
| `ExpressionShredder` | `ExpressionShredderThroughput` |
| `Items` | `ItemEvaluation` |

Scaling sets remain separate. For example, `-Set MetadataExpansion` excludes
`MetadataExpansionScaling`. The cross-cutting `Scaling` set contains both property- and
metadata-expansion scaling benchmarks.

`ExpressionShredderAllocations` is an opt-in cold-cache diagnostic for allocation-focused
shredder work and is not included in the broad `ExpressionShredder` set.

Common BenchmarkDotNet options are exposed directly:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Job short -DisableNGen
```

Use `-CollectEtw` or `-DisableInlining` for the other custom options. Less common
BenchmarkDotNet arguments can still be passed with `-BenchmarkDotNetArguments`.

Use `-All` to explicitly run every benchmark, or `-Framework` to override the target frameworks:

```powershell
.\Run-Benchmarks.ps1 -All
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Framework net11.0
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
## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*" --job short --disable-ngen
```
