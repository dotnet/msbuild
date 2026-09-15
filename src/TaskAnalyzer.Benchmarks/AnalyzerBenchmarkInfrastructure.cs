// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Benchmarks;

public sealed class AnalyzerScenario
{
    internal AnalyzerScenario(
        string name,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        bool includeCompilerDiagnostics = false)
    {
        Name = name;
        Analyzer = analyzer;
        DiagnosticId = diagnosticId;
        IncludeCompilerDiagnostics = includeCompilerDiagnostics;
    }

    public string Name { get; }

    internal DiagnosticAnalyzer Analyzer { get; }

    internal string DiagnosticId { get; }

    internal bool IncludeCompilerDiagnostics { get; }

    public override string ToString() => Name;
}

public sealed class AnalyzerDiagnosticScenario
{
    private readonly Func<int, string> _sourceFactory;

    internal AnalyzerDiagnosticScenario(
        string diagnosticId,
        DiagnosticAnalyzer analyzer,
        Func<int, string> sourceFactory)
    {
        DiagnosticId = diagnosticId;
        Analyzer = analyzer;
        _sourceFactory = sourceFactory;
    }

    public string DiagnosticId { get; }

    internal DiagnosticAnalyzer Analyzer { get; }

    internal string CreateSource(int diagnosticCount) => _sourceFactory(diagnosticCount);

    public override string ToString() => DiagnosticId;
}

internal sealed class AnalyzerRunner
{
    private static readonly CompilationWithAnalyzersOptions s_options = new(
        new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
        onAnalyzerException: static (exception, analyzer, _) =>
            throw new InvalidOperationException(
                $"Analyzer '{analyzer.GetType().FullName}' threw during benchmark execution.",
                exception),
        concurrentAnalysis: true,
        logAnalyzerExecutionTime: false,
        reportSuppressedDiagnostics: true);

    private readonly Compilation _compilation;
    private readonly ImmutableArray<DiagnosticAnalyzer> _analyzers;
    private readonly bool _includeCompilerDiagnostics;

    public AnalyzerRunner(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        bool includeCompilerDiagnostics)
    {
        _compilation = compilation;
        _analyzers = [analyzer];
        _includeCompilerDiagnostics = includeCompilerDiagnostics;
    }

    public ImmutableArray<Diagnostic> Run()
    {
        CompilationWithAnalyzers compilationWithAnalyzers = _compilation.WithAnalyzers(_analyzers, s_options);
        return _includeCompilerDiagnostics
            ? compilationWithAnalyzers.GetAllDiagnosticsAsync().GetAwaiter().GetResult()
            : compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}

internal static class AnalyzerBenchmarkValidation
{
    public static void Validate(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticId,
        int expectedCount,
        bool requireSuppressed)
    {
        int actualCount = 0;
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Id == diagnosticId &&
                (!requireSuppressed || diagnostic.IsSuppressed))
            {
                actualCount++;
            }
        }

        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Benchmark scenario expected {expectedCount} {diagnosticId} diagnostics, but produced {actualCount}.");
        }
    }
}

internal static class AnalyzerCompilation
{
    private static readonly CSharpParseOptions s_parseOptions = new(LanguageVersion.Preview);
    private static readonly MetadataReference[] s_references = CreateMetadataReferences();
    private static readonly SyntaxTree s_frameworkStubs = CSharpSyntaxTree.ParseText(
        AnalyzerSourceFactory.FrameworkStubs,
        s_parseOptions,
        path: "FrameworkStubs.cs");

    public static Compilation CreateWithoutMSBuildReferences() =>
        CreateCompilation(CSharpSyntaxTree.ParseText(
            "public sealed class EmptyCompilation { }",
            s_parseOptions,
            path: "Benchmark.cs"));

    public static Compilation CreateWithFrameworkStubs(string source) =>
        CreateCompilation(
            s_frameworkStubs,
            CSharpSyntaxTree.ParseText(source, s_parseOptions, path: "Benchmark.cs"));

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerBenchmark",
            syntaxTrees,
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        foreach (Diagnostic diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                throw new InvalidOperationException(
                    $"Benchmark source contains compiler error {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
        }

        return compilation;
    }

    private static MetadataReference[] CreateMetadataReferences()
    {
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");

        string[] paths = trustedPlatformAssemblies.Split(Path.PathSeparator);
        MetadataReference[] references = new MetadataReference[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            references[i] = MetadataReference.CreateFromFile(paths[i]);
        }

        return references;
    }
}
