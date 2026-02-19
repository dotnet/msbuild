// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                public string ProjectDirectory { get; }
                public string GetEnvironmentVariable(string name) => null;
                public void SetEnvironmentVariable(string name, string value) { }
                public System.Collections.IDictionary GetEnvironmentVariables() => null;
                public AbsolutePath GetAbsolutePath(string path) => default;
                public System.Diagnostics.ProcessStartInfo GetProcessStartInfo() => null;
            }

            public struct AbsolutePath
            {
                public string Value { get; }
                public string OriginalValue { get; }
                public static implicit operator string(AbsolutePath p) => p.Value;
            }

            public interface ITaskItem
            {
                string ItemSpec { get; set; }
                string GetMetadata(string metadataName);
            }

            public class TaskItem : ITaskItem
            {
                public string ItemSpec { get; set; }
                public string GetMetadata(string metadataName) => null;
                public string GetMetadataValue(string metadataName) => null;
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class MSBuildMultiThreadableTaskAnalyzedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class MSBuildMultiThreadableTaskAttribute : System.Attribute { }
        }

        namespace Microsoft.Build.Utilities
        {
            public abstract class Task : Microsoft.Build.Framework.ITask
            {
                public Microsoft.Build.Framework.IBuildEngine BuildEngine { get; set; }
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
