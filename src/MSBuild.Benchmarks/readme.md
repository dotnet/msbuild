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
dotnet run -c Release -f net10.0
```

### Filter to a Specific Benchmark Class

```
dotnet run -c Release -f net10.0 -- --filter "*LazyItemEvaluatorBenchmarks*"
```

### Filter to a Single Benchmark Method

```
dotnet run -c Release -f net10.0 -- --filter "*LazyItemEvaluatorBenchmarks.IncludeOnly"
```
## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net10.0 -- --filter "*LazyItemEvaluatorBenchmarks*" --job short --disable-ngen
```
