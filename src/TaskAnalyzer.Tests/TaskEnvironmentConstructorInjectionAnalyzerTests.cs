// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class TaskEnvironmentConstructorInjectionAnalyzerTests
{
    [Fact]
    public async Task ConcreteMultiThreadableTaskWithoutInjectingConstructor_ProducesInfoDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.PreferTaskEnvironmentConstructorInjection);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task PublicTaskEnvironmentConstructor_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
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
    public async Task PublicParameterlessAndTaskEnvironmentConstructors_DoNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public MyTask()
                {
                    TaskEnvironment = null!;
                }

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

    [Theory]
    [InlineData("private MyTask(TaskEnvironment taskEnvironment)")]
    [InlineData("public MyTask(TaskEnvironment taskEnvironment, string value)")]
    public async Task ConstructorNotUsableForInjection_ProducesDiagnostic(string constructorDeclaration)
    {
        var diagnostics = await GetDiagnosticsAsync($$"""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                {{constructorDeclaration}}
                {
                    TaskEnvironment = taskEnvironment;
                }

                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute() => true;
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PreferTaskEnvironmentConstructorInjection);
    }

    [Fact]
    public async Task AbstractMultiThreadableTask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegularTask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcreteTaskInheritingMultiThreadableImplementation_ProducesDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MultiThreadableTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }

            public class MyTask : MultiThreadableTask
            {
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    [Fact]
    public async Task InheritedTaskEnvironmentConstructor_ProducesDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public abstract class MultiThreadableTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                protected MultiThreadableTask()
                {
                    TaskEnvironment = null!;
                }

                protected MultiThreadableTask(TaskEnvironment taskEnvironment)
                {
                    TaskEnvironment = taskEnvironment;
                }

                public TaskEnvironment TaskEnvironment { get; set; }
            }

            public class MyTask : MultiThreadableTask
            {
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.GetMessage().ShouldContain("MyTask");
    }

    private static async Task<Diagnostic[]> GetDiagnosticsAsync(string source)
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            source,
            new TaskEnvironmentConstructorInjectionAnalyzer());

        return diagnostics
            .Where(diagnostic => diagnostic.Id == DiagnosticIds.PreferTaskEnvironmentConstructorInjection)
            .ToArray();
    }
}
