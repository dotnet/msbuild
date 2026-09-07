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
| `PropertyExpansionScaling` | Property-reference scaling and `PropertyBagCardinality` |
| `ItemExpansion` | Regular item expansion and `ItemFunctions` |
| `Conditions` | `ConditionParsing`, `ConditionEvaluation` |
| `ExpressionShredder` | `ExpressionShredderThroughput` |
| `Items` | `ItemEvaluation` |

Scaling sets remain separate. For example, `-Set MetadataExpansion` excludes
`MetadataExpansionScaling`. The cross-cutting `Scaling` set contains both property- and
metadata-expansion scaling benchmarks. `PropertyBagCardinality` varies the number of unreferenced
properties while keeping the referenced properties and expression shapes fixed.

`ExpressionShredderAllocations` is an opt-in cold-cache diagnostic for allocation-focused
shredder work and is not included in the broad `ExpressionShredder` set.

Common BenchmarkDotNet options are exposed directly:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Job short -DisableNGen
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -LaunchCount 3
```

Use `-CollectEtw`, `-DisableInlining`, or `-EnforcePowerPlan` for the other custom options.
Less common BenchmarkDotNet arguments can still be passed with `-BenchmarkDotNetArguments`.

Use `-All` to explicitly run every benchmark, or `-Framework` to override the target frameworks:

```powershell
.\Run-Benchmarks.ps1 -All
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Framework net11.0
```

### Choose a Run Mode

Use `-Job dry` or `-Job short` only to confirm that benchmarks build and execute. These jobs do
not collect enough data for performance conclusions.

For exploratory measurements, omit `-Job` and `-LaunchCount`. BenchmarkDotNet will adapt its
warmup and measurement iterations within one process launch. For a final comparison, use three
independent launches on the target framework:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" `
    -Framework net11.0 -LaunchCount 3
```

Each launch performs a complete benchmark run, so this approximately triples execution time.

The runner leaves the current OS power plan unchanged by default. Configure dedicated benchmark
machines with a stable performance-oriented power plan. Alternatively, use `-EnforcePowerPlan` to
allow BenchmarkDotNet to temporarily select the High Performance plan on Windows and restore the
previous plan when the run completes. If the process terminates abruptly, the plan may need to be
restored manually.

Compare results only when the target framework, architecture, runtime, and machine environment
match. In particular, absolute `net472` and `net11.0` results are not directly comparable.

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

## Evaluation Input Recording

`EvaluationInputRecordingBenchmark` measures what recording evaluation inputs
(`MSBUILDRECORDEVALUATIONINPUTS=1`) adds to an evaluation, in an isolated and in a shared evaluation
context. `EvaluationInputValidationBenchmark` separately measures checks of recorded files and
directories: unchanged, and after a project file, an import, or a glob directory changed.
Its evaluation and input capture happen outside the timed operations.

Both use the same synthetic project and any restored projects listed in
`MSBUILD_EVALUATION_INPUTS_BENCHMARK_PROJECTS` (path-separator
delimited). SDK-style projects also need `MSBUILD_EXE_PATH`, `MSBuildSDKsPath`, and
`DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` pointing at the bootstrap SDK, passed to the benchmark process
with `--envVars` as the shared fixture remarks describe. A project that is not cacheable fails setup with the
reason.

Run the observation cases or unchanged validation independently:

```powershell
.\src\MSBuild.Benchmarks\Run-Benchmarks.ps1 -Framework net11.0 -Filter '*EvaluationInputRecordingBenchmark.*'
.\src\MSBuild.Benchmarks\Run-Benchmarks.ps1 -Framework net11.0 -Filter '*EvaluationInputValidationBenchmark.ValidateUnchanged'
```

The two classes produce separate reports. To express validation cost relative to fresh evaluation,
compare `EvaluationInputValidationBenchmark.ValidateUnchanged` with
`EvaluationInputRecordingBenchmark.Evaluate` for the same project and run settings.
The stale-validation cases mutate files or directory membership; use synthetic or disposable workloads.

Validation compares path existence, file/directory kind, last-write timestamp, and length.
Glob membership is checked through the timestamps of the directories traversed.
Environment reads, SDK results, and registry reads remain recorded but are not revalidated, and the request key
is not compared. A successful filesystem check alone does not establish that an evaluation result
can be reused. Incomplete/non-cacheable recording and failed filesystem checks still reject validation.

Registry reads through `$(Registry:...)`, `[MSBuild]::GetRegistryValue`, and
`[MSBuild]::GetRegistryValueFromView` are retained in `EvaluationInputs.RegistryReads`.
Each observation contains the decoded key, value name (empty for the default value),
returned value, and the string view tokens the intrinsic considers (not ignored non-string arguments).
The key may be null for an accepted no-view request. Values are captured from the
existing call, not by rereading the registry; missing values remain `null`, while an intrinsic
that returns a supplied fallback records that fallback. Repeated reads are kept in order.
Binary, multi-string, and character arrays are copied into immutable arrays before further expansion.
Scalar string, primitive, enum, decimal, date/time, timespan, and GUID fallbacks are also supported.
Other fallback objects mark recording non-cacheable instead of retaining a mutable reference.
Registry access alone no longer stops recording. These are logical requests/results, not a
trace of each internal view probe or of resolver/toolset-internal registry accesses.

## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling
- `--enforce-power-plan` - Allow BenchmarkDotNet to select High Performance on Windows

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*" --job short --disable-ngen
```
