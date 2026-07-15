// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Benchmarks;

[MemoryDiagnoser]
public class AnalyzerNoOpBenchmark
{
    private AnalyzerRunner _runner = null!;

    public IEnumerable<AnalyzerNoOpScenario> Scenarios => AnalyzerScenarios.NoOpScenarios;

    [ParamsSource(nameof(Scenarios))]
    public AnalyzerNoOpScenario Scenario { get; set; } = null!;

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

public sealed class AnalyzerNoOpScenario
{
    internal AnalyzerNoOpScenario(
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

internal static class AnalyzerScenarios
{
    public static readonly AnalyzerNoOpScenario[] NoOpScenarios =
    [
        new(
            nameof(MultiThreadableTaskAnalyzer),
            new MultiThreadableTaskAnalyzer(),
            "MSBuildTask0001"),
        new(
            nameof(TransitiveCallChainAnalyzer),
            new TransitiveCallChainAnalyzer(),
            "MSBuildTask0005"),
        new(
            nameof(PreferTypedParameterAnalyzer),
            new PreferTypedParameterAnalyzer(),
            "MSBuildTask0006"),
        new(
            nameof(UnsupportedTaskItemTypeAnalyzer),
            new UnsupportedTaskItemTypeAnalyzer(),
            "MSBuildTask0009"),
        new(
            nameof(RequiredTaskPropertyInitializationSuppressor),
            new RequiredTaskPropertyInitializationSuppressor(),
            "CS8618",
            includeCompilerDiagnostics: true),
    ];

    public static readonly AnalyzerDiagnosticScenario[] DiagnosticScenarios =
    [
        new("MSBuildTask0001", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateCriticalErrorCalls),
        new("MSBuildTask0002", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateTaskEnvironmentCalls),
        new("MSBuildTask0003", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateRelativePathCalls),
        new("MSBuildTask0004", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreatePotentialIssueCalls),
        new("MSBuildTask0005", new TransitiveCallChainAnalyzer(), AnalyzerSourceFactory.CreateTransitiveCalls),
        new("MSBuildTask0006", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateTypedPathCandidates),
        new("MSBuildTask0007", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateTypedItemCandidates),
        new("MSBuildTask0008", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateRelativeDefaultCandidates),
        new("MSBuildTask0009", new UnsupportedTaskItemTypeAnalyzer(), AnalyzerSourceFactory.CreateUnsupportedItemTypes),
    ];
}

internal sealed class AnalyzerRunner
{
    private static readonly CompilationWithAnalyzersOptions s_options = new(
        new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
        onAnalyzerException: null,
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

internal static class AnalyzerSourceFactory
{
    public const string FrameworkStubs = """
        namespace Microsoft.Build.Framework
        {
            public interface IBuildEngine { }

            public interface ITask
            {
                IBuildEngine BuildEngine { get; set; }
                bool Execute();
            }

            public interface IMultiThreadableTask : ITask
            {
                TaskEnvironment TaskEnvironment { get; set; }
            }

            public sealed class TaskEnvironment
            {
                public AbsolutePath ProjectDirectory => default;
                public string? GetEnvironmentVariable(string name) => null;
                public AbsolutePath GetAbsolutePath(string path) => default;
            }

            public readonly struct AbsolutePath
            {
                public AbsolutePath(string path) { }
            }

            public interface ITaskItem
            {
                string ItemSpec { get; set; }
            }

            public interface ITaskItem<T> : ITaskItem
            {
                T Value { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class MSBuildMultiThreadableTaskAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class MSBuildMultiThreadableTaskAnalyzedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class OutputAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class RequiredAttribute : System.Attribute { }
        }

        namespace Microsoft.Build.Utilities
        {
            public abstract class Task : Microsoft.Build.Framework.ITask
            {
                public Microsoft.Build.Framework.IBuildEngine BuildEngine { get; set; } = null!;
                public abstract bool Execute();
            }
        }
        """;

    public static string CreateCriticalErrorCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Console.WriteLine(\"benchmark\");",
            additionalMembers: null);

    public static string CreateTaskEnvironmentCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Environment.GetEnvironmentVariable(\"BENCHMARK\");",
            additionalMembers: null);

    public static string CreateRelativePathCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.IO.File.Exists(\"relative.txt\");",
            additionalMembers: null);

    public static string CreatePotentialIssueCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Reflection.Assembly.LoadFrom(\"plugin.dll\");",
            additionalMembers: null);

    public static string CreateTypedPathCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(InputPath);",
            "public string InputPath { get; set; } = \"\";",
            addMultiThreadableAttribute: true);

    public static string CreateTypedItemCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(Input.ItemSpec);",
            "public Microsoft.Build.Framework.ITaskItem Input { get; set; } = null!;",
            addMultiThreadableAttribute: true);

    public static string CreateRelativeDefaultCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(InputPath);",
            "public string InputPath { get; set; } = \"obj\";",
            addMultiThreadableAttribute: true);

    public static string CreateUnsupportedItemTypes(int count) =>
        CreateTaskClasses(
            count,
            operation: null,
            "public Microsoft.Build.Framework.ITaskItem<int> Input { get; set; } = null!;");

    public static string CreateTransitiveCalls(int count)
    {
        StringBuilder source = new();
        for (int i = 0; i < count; i++)
        {
            source.AppendLine($$"""
                public static class BenchmarkHelper{{i}}
                {
                    public static void Run() => System.Console.WriteLine("benchmark");
                }

                public sealed class BenchmarkTask{{i}} : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute()
                    {
                        BenchmarkHelper{{i}}.Run();
                        return true;
                    }
                }

                """);
        }

        return source.ToString();
    }

    public static string CreateRequiredProperties(int count)
    {
        StringBuilder source = new(
            """
            public sealed class BenchmarkTask : Microsoft.Build.Utilities.Task
            {

            """);

        for (int i = 0; i < count; i++)
        {
            source.AppendLine($$"""
                    [Microsoft.Build.Framework.Required]
                    public string Input{{i}} { get; set; }

                """);
        }

        source.AppendLine(
            """
                public override bool Execute() => true;
            }
            """);

        return source.ToString();
    }

    private static string CreateTaskClasses(
        int count,
        string? operation,
        string? additionalMembers,
        bool addMultiThreadableAttribute = false)
    {
        StringBuilder source = new();
        for (int i = 0; i < count; i++)
        {
            if (addMultiThreadableAttribute)
            {
                source.AppendLine("[Microsoft.Build.Framework.MSBuildMultiThreadableTask]");
            }

            source.AppendLine($"public sealed class BenchmarkTask{i} : Microsoft.Build.Utilities.Task");
            source.AppendLine("{");
            if (additionalMembers is not null)
            {
                source.Append("    ");
                source.AppendLine(additionalMembers);
            }

            source.AppendLine("    public override bool Execute()");
            source.AppendLine("    {");
            if (operation is not null)
            {
                source.Append("        ");
                source.AppendLine(operation);
            }

            source.AppendLine("        return true;");
            source.AppendLine("    }");
            source.AppendLine("}");
        }

        return source.ToString();
    }
}
