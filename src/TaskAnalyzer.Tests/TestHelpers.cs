// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Helpers for setting up analyzer tests with MSBuild Framework stubs.
/// Uses manual compilation + analyzer invocation rather than CSharpAnalyzerTest
/// to avoid strict message argument comparison issues with nullable annotations.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Minimal stubs for ITask, IMultiThreadableTask, TaskEnvironment, AbsolutePath, and ITaskItem.
    /// </summary>
    public const string FrameworkStubs = """
        namespace Microsoft.Build.Framework
        {
            public interface IBuildEngine { }

            public sealed class BuildEngineStub : IBuildEngine { }

            public interface ITask
            {
                IBuildEngine BuildEngine { get; set; }
                bool Execute();
            }

            public interface IMultiThreadableTask : ITask
            {
                TaskEnvironment TaskEnvironment { get; set; }
            }

            public class TaskEnvironment
            {
                public AbsolutePath ProjectDirectory => default;
                public string? GetEnvironmentVariable(string name) => null;
                public void SetEnvironmentVariable(string name, string? value) { }
                public System.Collections.Generic.IReadOnlyDictionary<string, string> GetEnvironmentVariables() => new System.Collections.Generic.Dictionary<string, string>();
                public AbsolutePath GetAbsolutePath(string path) => default;
                public System.Diagnostics.ProcessStartInfo GetProcessStartInfo() => new();
            }

            public struct AbsolutePath : System.IEquatable<AbsolutePath>
            {
                public AbsolutePath(string path) { Value = path; OriginalValue = path; }
                public string Value { get; }
                public string OriginalValue { get; }
                public static implicit operator string(AbsolutePath p) => p.Value;
                public bool Equals(AbsolutePath other) => Value == other.Value;
                public override bool Equals(object? obj) => obj is AbsolutePath other && Equals(other);
                public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();
                public static bool operator ==(AbsolutePath left, AbsolutePath right) => left.Equals(right);
                public static bool operator !=(AbsolutePath left, AbsolutePath right) => !left.Equals(right);
            }

            public interface ITaskItem
            {
                string ItemSpec { get; set; }
                string GetMetadata(string metadataName);
            }

            public interface ITaskItem2 : ITaskItem
            {
            }

            public interface ITaskItem<T> : ITaskItem2
            {
                T Value { get; }
            }

            public class TaskItem : ITaskItem
            {
                public string ItemSpec { get; set; } = string.Empty;
                public string GetMetadata(string metadataName) => string.Empty;
                public string GetMetadataValue(string metadataName) => string.Empty;
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class OutputAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class MSBuildMultiThreadableTaskAnalyzedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class MSBuildMultiThreadableTaskAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class RequiredAttribute : System.Attribute { }
        }

        namespace System.ComponentModel.DataAnnotations
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class RequiredAttribute : System.Attribute { }
        }

        namespace Microsoft.Build.Utilities
        {
            public abstract class Task : Microsoft.Build.Framework.ITask
            {
                public Microsoft.Build.Framework.IBuildEngine BuildEngine { get; set; } = new Microsoft.Build.Framework.BuildEngineStub();
                public abstract bool Execute();
            }

            public abstract class ToolTask : Task
            {
                protected abstract string ToolName { get; }
                protected abstract string GenerateFullPathToTool();
            }
        }
        """;

    private static readonly MetadataReference[] s_coreReferences = CreateCoreReferences();

    /// <summary>
    /// Returns a fully-qualified path literal that is absolute on the OS the tests are running on: a
    /// drive-rooted path on Windows (<c>C:/…</c>) and a leading-slash path on Unix (<c>/…</c>). Use this when
    /// analyzer test source needs a default value that must classify as fully-qualified regardless of OS —
    /// a single hard-coded literal cannot satisfy both, since <c>C:/x</c> is relative on Unix and <c>/x</c> is
    /// not fully-qualified on Windows.
    /// </summary>
    public static string FullyQualifiedPath(string tail) =>
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "C:/" + tail
            : "/" + tail;

    /// <summary>
    /// Returns the core runtime references used by test compilations.
    /// </summary>
    public static MetadataReference[] GetCoreReferences() => s_coreReferences;

    /// <summary>
    /// Runs the MultiThreadableTaskAnalyzer on the given source code and returns analyzer diagnostics.
    /// Source is combined with framework stubs automatically.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new MultiThreadableTaskAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var allDiags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return allDiags;
    }

    /// <summary>
    /// Runs BOTH the direct and transitive analyzers on the given source code.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetAllDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MultiThreadableTaskAnalyzer(),
            new TransitiveCallChainAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        var allDiags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return allDiags;
    }

    /// <summary>
    /// Runs compiler diagnostics together with analyzers and suppressors and returns
    /// diagnostics reported for the primary test source file.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetCompilerAndAnalyzerDiagnosticsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        var compilation = CreateCompilation(source);
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);

        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzers), options);
        var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        return allDiagnostics
            .Where(d => d.Location.SourceTree?.FilePath == "Test.cs")
            .ToImmutableArray();
    }

    /// <summary>
    /// Runs the PreferTypedParameterAnalyzer on the given source code and returns analyzer diagnostics.
    /// Source is combined with framework stubs automatically.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetTypedParameterDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new PreferTypedParameterAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var allDiags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return allDiags;
    }

    /// <summary>
    /// Runs the UnsupportedTaskItemTypeAnalyzer on the given source code and returns analyzer diagnostics.
    /// Source is combined with framework stubs automatically.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetUnsupportedTaskItemTypeDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new UnsupportedTaskItemTypeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var allDiags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return allDiags;
    }

    /// <summary>
    /// Creates a compilation with the given source code and framework stubs.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(source, path: "Test.cs"),
            CSharpSyntaxTree.ParseText(FrameworkStubs, path: "Stubs.cs"),
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            s_coreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    /// <summary>
    /// Runs the MultiThreadableTaskAnalyzer with a specific scope option and returns analyzer diagnostics.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> GetDiagnosticsWithScopeAsync(string source, string scope)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new MultiThreadableTaskAnalyzer();

        var globalOptions = new Dictionary<string, string>
        {
            { $"build_property.{SharedAnalyzerHelpers.ScopeOptionKey}", scope }
        };
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer), options);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static MetadataReference[] CreateCoreReferences()
    {
        // Reference the core runtime assemblies needed
        var assemblies = new[]
        {
            typeof(object).Assembly,                          // System.Runtime / mscorlib
            typeof(System.Console).Assembly,                  // System.Console
            typeof(System.IO.File).Assembly,                  // System.IO.FileSystem
            typeof(System.IO.FileInfo).Assembly,              // System.IO.FileSystem
            typeof(System.IO.StreamReader).Assembly,          // System.IO
            typeof(System.IO.FileStream).Assembly,            // System.IO.FileSystem
            typeof(System.Diagnostics.Process).Assembly,      // System.Diagnostics.Process
            typeof(System.Diagnostics.ProcessStartInfo).Assembly,
            typeof(System.Reflection.Assembly).Assembly,      // System.Reflection
            typeof(System.Threading.ThreadPool).Assembly,     // System.Threading.ThreadPool
            typeof(System.Globalization.CultureInfo).Assembly, // System.Globalization
            typeof(System.Collections.IDictionary).Assembly,  // System.Collections
            typeof(System.Collections.Generic.List<>).Assembly,
            typeof(System.Linq.Enumerable).Assembly,          // System.Linq
            typeof(System.Threading.Tasks.Task).Assembly,     // System.Threading.Tasks
            typeof(System.Runtime.InteropServices.GuidAttribute).Assembly, // System.Runtime
            typeof(System.Xml.Linq.XDocument).Assembly,       // System.Xml.Linq
            typeof(System.Xml.XmlReader).Assembly,            // System.Xml.ReaderWriter
            typeof(System.IO.Compression.ZipFile).Assembly,   // System.IO.Compression.ZipFile
            typeof(System.IO.Compression.ZipArchive).Assembly, // System.IO.Compression
        };

        var locations = assemblies
            .Select(a => a.Location)
            .Distinct()
            .ToList();

        // Ensure System.Runtime is included (needed for Emit on .NET 10+)
        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir is not null)
        {
            var systemRuntime = System.IO.Path.Combine(runtimeDir, "System.Runtime.dll");
            if (System.IO.File.Exists(systemRuntime) && !locations.Contains(systemRuntime))
            {
                locations.Add(systemRuntime);
            }
        }

        return locations
            .Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc))
            .ToArray();
    }
}

/// <summary>
/// A test implementation of <see cref="AnalyzerConfigOptionsProvider"/> that returns
/// configurable global options for testing scope and other analyzer settings.
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
    {
        _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestAnalyzerConfigOptions.Empty;

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static readonly TestAnalyzerConfigOptions Empty = new(new Dictionary<string, string>());

        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) => _options.TryGetValue(key, out value);
    }
}
