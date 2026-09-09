// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class MultiThreadableTaskDeclarationAnalyzerTests
{
    [Fact]
    public async Task AttributeWithTaskEnvironmentPropertyButNoInterface_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.TaskEnvironmentNeverAssigned);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    /// <summary>
    /// The attribute alone is the supported compatibility-bridge state: it declares the task safe to
    /// run in-process without giving it access to TaskEnvironment. Nothing is wrong here.
    /// </summary>
    [Fact]
    public async Task AttributeWithoutTaskEnvironmentProperty_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string Value { get; set; } = "";
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The engine selects a single-parameter TaskEnvironment constructor by signature alone,
    /// independently of IMultiThreadableTask, so this task does receive an environment.
    /// </summary>
    [Fact]
    public async Task AttributeWithTaskEnvironmentConstructorButNoInterface_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public MyTask(TaskEnvironment taskEnvironment)
                {
                    TaskEnvironment = taskEnvironment;
                }

                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task FullyMigratedTask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Interface without the attribute is a valid intermediate state, so the rule reporting it is
    /// disabled by default.
    /// </summary>
    [Fact]
    public async Task InterfaceWithoutAttribute_DoesNotProduceDiagnosticByDefault()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The TaskEnvironment property is inherited from an unannotated base. The base does not implement
    /// the interface either, so the engine still never assigns it.
    /// </summary>
    [Fact]
    public async Task InheritedTaskEnvironmentPropertyWithoutInterface_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MyTaskBase : Microsoft.Build.Utilities.Task
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            [MSBuildMultiThreadableTask]
            public class MyTask : MyTaskBase
            {
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.TaskEnvironmentNeverAssigned);
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    /// <summary>
    /// The interface is satisfied through the base type, so injection works.
    /// </summary>
    [Fact]
    public async Task InterfaceImplementedOnBase_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MyTaskBase : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            [MSBuildMultiThreadableTask]
            public class MyTask : MyTaskBase
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The attribute is not inherited (AttributeUsage sets Inherited = false) and TaskRouter reads it
    /// with inherit: false, so a derived task does not pick it up from its base. Only the base is
    /// reported -- the derived task itself is clean, which is precisely why the diagnostic is useful:
    /// nothing else signals that the derived task is still routed to a TaskHost.
    /// </summary>
    [Fact]
    public async Task AttributeOnAbstractBase_ReportsOnlyTheBase()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public abstract class MyTaskBase : Microsoft.Build.Utilities.Task
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            public class MyTask : MyTaskBase
            {
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect);
        diagnostic.GetMessage().ShouldContain("MyTaskBase");
    }

    /// <summary>
    /// TaskRouter reads the attribute with inherit: false off the concrete type it instantiated, and it
    /// never instantiates an abstract one, so the attribute reaches no derived task.
    /// </summary>
    [Fact]
    public async Task AttributeOnAbstractTask_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public abstract class MyTask : Microsoft.Build.Utilities.Task
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("MyTask");
        diagnostic.GetMessage().ShouldContain("not inherited");
    }

    /// <summary>
    /// The correct shape: the shared base carries no attribute and each concrete task applies its own.
    /// </summary>
    [Fact]
    public async Task AbstractTaskWithoutAttribute_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MyTaskBase : Microsoft.Build.Utilities.Task
            {
                public string Shared { get; set; } = "";
            }

            [MSBuildMultiThreadableTask]
            public class MyTask : MyTaskBase
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// TaskRouter only inspects types the engine is about to run as a task, so the attribute does
    /// nothing on a type that is not one.
    /// </summary>
    [Fact]
    public async Task AttributeOnNonTaskType_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class NotATask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("NotATask");
    }

    /// <summary>
    /// The shape the rule is really aimed at: the attribute lands on a helper type in a file that also
    /// declares the task, so the task itself is left unmarked and still routed to a TaskHost.
    /// </summary>
    [Fact]
    public async Task AttributeOnHelperTypeBesideTask_ReportsOnlyTheHelper()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTaskHelper
            {
                public string Value { get; set; } = "";
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect);
        diagnostic.GetMessage().ShouldContain("MyTaskHelper");
    }

    [Fact]
    public async Task NonTaskTypeWithoutAttribute_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class NotATask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadOnlyTaskEnvironmentProperty_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public TaskEnvironment TaskEnvironment => TaskEnvironment.Fallback;
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlainTask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// ToolTask implements IMultiThreadableTask and declares a TaskEnvironment property, so every
    /// ToolTask-derived task satisfies the interface without its author declaring anything. Reporting
    /// those would flag thousands of untouched tasks. Runs with the rule explicitly enabled so the
    /// assertion cannot pass merely because the rule is off by default.
    /// </summary>
    [Fact]
    public async Task InterfaceInheritedFromToolTaskLikeBase_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsWithMissingAttributeRuleEnabledAsync("""
            using Microsoft.Build.Framework;

            public abstract class ToolTaskLike : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public virtual TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            public class MyTask : ToolTaskLike
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// A ToolTask-derived task carrying the attribute is fully migrated: the interface and the
    /// TaskEnvironment property both arrive through the base.
    /// </summary>
    [Fact]
    public async Task AttributeOnToolTaskLikeDerivedTask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsWithMissingAttributeRuleEnabledAsync("""
            using Microsoft.Build.Framework;

            public abstract class ToolTaskLike : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public virtual TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            [MSBuildMultiThreadableTask]
            public class MyTask : ToolTaskLike
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Declaring the interface in the type's own base list is an explicit opt-in, so the missing
    /// attribute is worth reporting once the rule is enabled.
    /// </summary>
    [Fact]
    public async Task DeclaredInterfaceWithoutAttribute_ProducesInfo_WhenRuleEnabled()
    {
        var diagnostics = await GetDiagnosticsWithMissingAttributeRuleEnabledAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.MissingMultiThreadableTaskAttribute);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    /// <summary>
    /// The attribute is matched by full name, mirroring the engine, so these rules keep working when a
    /// repository's own copy of the attribute makes the name ambiguous and unresolvable as a symbol.
    /// See SharedAnalyzerHelpers.HasMultiThreadableTaskAttribute.
    /// </summary>
    [Fact]
    public async Task AttributeFromReferencedAssembly_OnNonTaskType_ProducesWarning()
    {
        CSharpCompilation compilation = CreateCompilationWithAttributeFromReferences("""
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class PathHelper
            {
                public string Combine(string a, string b) => a + b;
            }
            """);

        // The premise of the name-based matching: the symbol is unresolvable here.
        compilation.GetTypeByMetadataName("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute").ShouldBeNull();

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MultiThreadableTaskDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Diagnostic diagnostic = diagnostics
            .Single(d => d.Id == DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect);
        diagnostic.GetMessage().ShouldContain("PathHelper");
    }

    private static async Task<Diagnostic[]> GetDiagnosticsWithMissingAttributeRuleEnabledAsync(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new[]
            {
                new KeyValuePair<string, ReportDiagnostic>(
                    DiagnosticIds.MissingMultiThreadableTaskAttribute,
                    ReportDiagnostic.Info),
            }));

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MultiThreadableTaskDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics
            .Where(diagnostic =>
                diagnostic.Id == DiagnosticIds.TaskEnvironmentNeverAssigned ||
                diagnostic.Id == DiagnosticIds.MissingMultiThreadableTaskAttribute)
            .ToArray();
    }

    private static async Task<Diagnostic[]> GetDiagnosticsAsync(string source)
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            source,
            new MultiThreadableTaskDeclarationAnalyzer());

        return diagnostics
            .Where(diagnostic =>
                diagnostic.Id == DiagnosticIds.TaskEnvironmentNeverAssigned ||
                diagnostic.Id == DiagnosticIds.MissingMultiThreadableTaskAttribute ||
                diagnostic.Id == DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect)
            .ToArray();
    }
}
