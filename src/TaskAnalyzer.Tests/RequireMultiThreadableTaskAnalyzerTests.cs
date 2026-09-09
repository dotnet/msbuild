// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class RequireMultiThreadableTaskAnalyzerTests
{
    private const string SeverityOptionKey = "dotnet_diagnostic." + DiagnosticIds.RequireMultiThreadableTask + ".severity";

    private const string MarkedUpTaskWithoutOptIn = """
        public class {|#0:MyTask|} : Microsoft.Build.Utilities.Task
        {
            public override bool Execute() => true;
        }
        """;

    private const string ConcreteTaskWithoutOptIn = """
        public class MyTask : Microsoft.Build.Utilities.Task
        {
            public override bool Execute() => true;
        }
        """;

    [Fact]
    public async Task RequireScope_ConcreteTaskWithoutAttribute_ProducesDiagnostic()
    {
        var diagnostics = await GetDiagnosticsForScopeAsync(ConcreteTaskWithoutOptIn, SharedAnalyzerHelpers.ScopeRequireMultiThreadable);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.RequireMultiThreadableTask);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task RequireScope_ThroughBuildProperty_ProducesDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync(
            ConcreteTaskWithoutOptIn,
            new Dictionary<string, string>
            {
                { $"build_property.{SharedAnalyzerHelpers.ScopeOptionKey}", SharedAnalyzerHelpers.ScopeRequireMultiThreadable },
            });

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.RequireMultiThreadableTask);
    }

    [Fact]
    public async Task NoScopeConfigured_ConcreteTaskWithoutAttribute_ProducesNoDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync(ConcreteTaskWithoutOptIn, []);

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SharedAnalyzerHelpers.ScopeAll)]
    [InlineData(SharedAnalyzerHelpers.ScopeMultiThreadableOnly)]
    public async Task OtherScopes_ConcreteTaskWithoutAttribute_ProduceNoDiagnostic(string scope)
    {
        var diagnostics = await GetDiagnosticsForScopeAsync(ConcreteTaskWithoutOptIn, scope);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoConfiguration_ProducesNoDiagnostic()
    {
        await CreateAnalyzerTest(ConcreteTaskWithoutOptIn, analyzerConfig: null).RunAsync();
    }

    [Fact]
    public async Task EditorConfigSeverity_WithoutScope_ProducesDiagnostic()
    {
        await CreateAnalyzerTest(
            MarkedUpTaskWithoutOptIn,
            analyzerConfig: ("/.editorconfig", $"""
                root = true
                [*.cs]
                {SeverityOptionKey} = warning
                """),
            new DiagnosticResult(DiagnosticDescriptors.RequireMultiThreadableTask).WithLocation(0).WithArguments("MyTask")).RunAsync();
    }

    [Fact]
    public async Task EditorConfigSeverityNone_WithoutScope_ProducesNoDiagnostic()
    {
        await CreateAnalyzerTest(
            ConcreteTaskWithoutOptIn,
            analyzerConfig: ("/.editorconfig", $"""
                root = true
                [*.cs]
                {SeverityOptionKey} = none
                """)).RunAsync();
    }

    [Fact]
    public async Task EditorConfigSeverityOnOneFileOfPartialTask_ProducesDiagnosticThere()
    {
        var test = CreateAnalyzerTest(
            """
            public partial class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """,
            analyzerConfig: ("/.editorconfig", $"""
                root = true
                [Other.cs]
                {SeverityOptionKey} = warning
                """),
            new DiagnosticResult(DiagnosticDescriptors.RequireMultiThreadableTask).WithLocation(0).WithArguments("MyTask"));
        test.TestState.Sources.Add(("/0/Other.cs", """
            public partial class {|#0:MyTask|}
            {
            }
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task RulesetSeverity_WithoutScope_ProducesDiagnostic()
    {
        // A ruleset or <WarningsAsErrors> entry surfaces as a compilation-wide specific diagnostic option.
        Compilation compilation = CreateCompilation(ConcreteTaskWithoutOptIn);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(DiagnosticIds.RequireMultiThreadableTask, ReportDiagnostic.Error)));

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RequireMultiThreadableTaskAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.RequireMultiThreadableTask);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task RequireScope_TaskWithAttribute_ProducesNoDiagnostic()
    {
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequireScope_AbstractTask_ProducesNoDiagnostic()
    {
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            public abstract class MyTask : Microsoft.Build.Utilities.Task
            {
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequireScope_NonTaskClass_ProducesNoDiagnostic()
    {
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            public class Helper
            {
                public bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequireScope_ConcreteTaskDerivedFromAnnotatedBase_ProducesDiagnostic()
    {
        // The attribute is Inherited = false, so the leaf type has not opted in and the engine still
        // routes it to a TaskHost — the mistake this rule exists to catch.
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public abstract class MultiThreadableTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            public class MyTask : MultiThreadableTask
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.Single().GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task RequireScope_MultiThreadableTaskWithoutAttribute_ProducesDiagnostic()
    {
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diagnostics.Single().GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task RequireScope_SameNamedAttributeFromAnotherNamespace_ProducesDiagnostic()
    {
        // The engine matches the attribute by namespace and name, so an unrelated attribute that merely
        // shares the name is not an opt-in.
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            namespace Contoso
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class MSBuildMultiThreadableTaskAttribute : System.Attribute
                {
                }

                [MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute() => true;
                }
            }
            """);

        diagnostics.Single().GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task RequireScope_NestedConcreteTask_ProducesDiagnostic()
    {
        var diagnostics = await GetRequiredDiagnosticsAsync("""
            public class Outer
            {
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute() => true;
                }
            }
            """);

        diagnostics.Single().GetMessage().ShouldContain("MyTask");
    }

    /// <summary>
    /// The engine matches the attribute by full name and ignores the defining assembly, so a task marked with a
    /// repository's own copy really is routed in-process and must not be told to opt in. That copy also makes the
    /// name ambiguous, so the attribute cannot be resolved as a symbol at all -- the reason this rule and its
    /// siblings match by name. See SharedAnalyzerHelpers.HasMultiThreadableTaskAttribute.
    /// </summary>
    [Fact]
    public async Task RequireScope_AttributeFromReferencedAssembly_ProducesNoDiagnostic()
    {
        var compilation = TestHelpers.CreateCompilationWithAttributeFromReferences("""
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        // The premise of the rule's name-based matching: the symbol is unresolvable here.
        compilation.GetTypeByMetadataName("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute").ShouldBeNull();

        var diagnostics = await TestHelpers.GetDiagnosticsWithGlobalOptionsAsync(
            compilation,
            new RequireMultiThreadableTaskAnalyzer(),
            new Dictionary<string, string>
            {
                { SharedAnalyzerHelpers.ScopeOptionKey, SharedAnalyzerHelpers.ScopeRequireMultiThreadable },
            });

        diagnostics
            .Where(diagnostic => diagnostic.Id == DiagnosticIds.RequireMultiThreadableTask)
            .ShouldBeEmpty();
    }

    private static CSharpAnalyzerTest<RequireMultiThreadableTaskAnalyzer, DefaultVerifier> CreateAnalyzerTest(
        string source, (string Path, string Content)? analyzerConfig, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<RequireMultiThreadableTaskAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        if (analyzerConfig is (string path, string content))
        {
            test.TestState.AnalyzerConfigFiles.Add((path, content));
        }

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static Task<Diagnostic[]> GetRequiredDiagnosticsAsync(string source) =>
        GetDiagnosticsAsync(
            source,
            new Dictionary<string, string>
            {
                { SharedAnalyzerHelpers.ScopeOptionKey, SharedAnalyzerHelpers.ScopeRequireMultiThreadable },
            });

    private static async Task<Diagnostic[]> GetDiagnosticsForScopeAsync(string source, string scope) =>
        await GetDiagnosticsAsync(
            source,
            new Dictionary<string, string> { { SharedAnalyzerHelpers.ScopeOptionKey, scope } });

    private static async Task<Diagnostic[]> GetDiagnosticsAsync(string source, Dictionary<string, string> globalOptions)
    {
        var diagnostics = await GetDiagnosticsWithGlobalOptionsAsync(
            source,
            new RequireMultiThreadableTaskAnalyzer(),
            globalOptions);

        return diagnostics
            .Where(diagnostic => diagnostic.Id == DiagnosticIds.RequireMultiThreadableTask)
            .ToArray();
    }
}
