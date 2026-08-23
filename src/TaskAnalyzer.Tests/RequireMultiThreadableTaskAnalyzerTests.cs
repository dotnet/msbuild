// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class RequireMultiThreadableTaskAnalyzerTests
{
    private const string SeverityOptionKey = "dotnet_diagnostic." + DiagnosticIds.RequireMultiThreadableTask + ".severity";

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

    [Theory]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("suggestion")]
    public async Task ExplicitSeverity_WithoutScope_ProducesDiagnostic(string severity)
    {
        var diagnostics = await GetDiagnosticsAsync(
            ConcreteTaskWithoutOptIn,
            new Dictionary<string, string> { { SeverityOptionKey, severity } });

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.RequireMultiThreadableTask);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("default")]
    public async Task SeverityConfiguredOff_WithoutScope_ProducesNoDiagnostic(string severity)
    {
        var diagnostics = await GetDiagnosticsAsync(
            ConcreteTaskWithoutOptIn,
            new Dictionary<string, string> { { SeverityOptionKey, severity } });

        diagnostics.ShouldBeEmpty();
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
