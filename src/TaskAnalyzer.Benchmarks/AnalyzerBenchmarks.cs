// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Benchmarks;

[MemoryDiagnoser]
public class AnalyzerNoOpBenchmark
{
    private AnalyzerRunner _runner = null!;

    public IEnumerable<AnalyzerScenario> Scenarios => AnalyzerScenarios.Analyzers;

    [ParamsSource(nameof(Scenarios))]
    public AnalyzerScenario Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _runner = new AnalyzerRunner(
            AnalyzerCompilation.CreateWithoutMSBuildReferences(),
            Scenario.Analyzer,
            includeCompilerDiagnostics: Scenario.IncludeCompilerDiagnostics);

        AnalyzerBenchmarkValidation.Validate(
            _runner.Run(),
            Scenario.DiagnosticId,
            expectedCount: 0,
            requireSuppressed: false);
    }

    [Benchmark]
    public ImmutableArray<Diagnostic> NoOp() => _runner.Run();
}

[MemoryDiagnoser]
public class AnalyzerCompliantTaskBenchmark
{
    private AnalyzerRunner _runner = null!;

    public IEnumerable<AnalyzerScenario> Scenarios => AnalyzerScenarios.Analyzers;

    [ParamsSource(nameof(Scenarios))]
    public AnalyzerScenario Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _runner = new AnalyzerRunner(
            AnalyzerCompilation.CreateWithFrameworkStubs(AnalyzerSourceFactory.CompliantTask),
            Scenario.Analyzer,
            includeCompilerDiagnostics: Scenario.IncludeCompilerDiagnostics);

        AnalyzerBenchmarkValidation.Validate(
            _runner.Run(),
            Scenario.DiagnosticId,
            expectedCount: 0,
            requireSuppressed: false);
    }

    [Benchmark]
    public ImmutableArray<Diagnostic> AnalyzeCompliantTask() => _runner.Run();
}

[MemoryDiagnoser]
public class AnalyzerDiagnosticBenchmark
{
    private AnalyzerRunner _runner = null!;

    public IEnumerable<AnalyzerDiagnosticScenario> Scenarios => AnalyzerScenarios.DiagnosticScenarios;

    [ParamsSource(nameof(Scenarios))]
    public AnalyzerDiagnosticScenario Scenario { get; set; } = null!;

    [Params(1, 10, 100)]
    public int DiagnosticCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runner = new AnalyzerRunner(
            AnalyzerCompilation.CreateWithFrameworkStubs(Scenario.CreateSource(DiagnosticCount)),
            Scenario.Analyzer,
            includeCompilerDiagnostics: false);

        AnalyzerBenchmarkValidation.Validate(
            _runner.Run(),
            Scenario.DiagnosticId,
            DiagnosticCount,
            requireSuppressed: false);
    }

    [Benchmark]
    public ImmutableArray<Diagnostic> AnalyzeDiagnostics() => _runner.Run();
}

[MemoryDiagnoser]
public class AnalyzerSuppressorBenchmark
{
    private AnalyzerRunner _runner = null!;

    [Params(1, 10, 100)]
    public int DiagnosticCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runner = new AnalyzerRunner(
            AnalyzerCompilation.CreateWithFrameworkStubs(AnalyzerSourceFactory.CreateRequiredProperties(DiagnosticCount)),
            new RequiredTaskPropertyInitializationSuppressor(),
            includeCompilerDiagnostics: true);

        AnalyzerBenchmarkValidation.Validate(
            _runner.Run(),
            diagnosticId: "CS8618",
            DiagnosticCount,
            requireSuppressed: true);
    }

    [Benchmark]
    public ImmutableArray<Diagnostic> SuppressDiagnostics() => _runner.Run();
}
