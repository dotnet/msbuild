// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class TaskEnvironmentPropagationAnalyzerTests
{
    private const string InnerTasks = """
        public class InnerTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public Microsoft.Build.Framework.TaskEnvironment TaskEnvironment { get; set; } = null!;
            public override bool Execute() => true;
        }

        public class LegacyTask : Microsoft.Build.Utilities.Task
        {
            public override bool Execute() => true;
        }
        """;

    [Fact]
    public async Task ConstructedTaskWithoutTaskEnvironment_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new InnerTask { BuildEngine = BuildEngine };
                    return inner.Execute();
                }
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("InnerTask");
    }

    [Fact]
    public async Task ConstructedToolTaskWithoutTaskEnvironment_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                private InnerToolTask? _running;

                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    _running = new InnerToolTask
                    {
                        BuildEngine = BuildEngine,
                    };

                    return _running.Execute();
                }
            }

            public class InnerToolTask : Microsoft.Build.Utilities.ToolTask
            {
                protected override string ToolName => "tool";
                protected override string GenerateFullPathToTool() => "tool";
                public override bool Execute() => true;
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    [Fact]
    public async Task ImplicitObjectCreation_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    InnerTask inner = new();
                    return inner.Execute();
                }
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    [Fact]
    public async Task TaskEnvironmentInObjectInitializer_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new InnerTask { BuildEngine = BuildEngine, TaskEnvironment = TaskEnvironment };
                    return inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task TaskEnvironmentAssignedAfterCreation_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new InnerTask();
                    inner.BuildEngine = BuildEngine;
                    inner.TaskEnvironment = TaskEnvironment;
                    return inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task TaskEnvironmentAssignedToFieldInAnotherMethod_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                private InnerTask? _inner;

                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    _inner = new InnerTask { BuildEngine = BuildEngine };
                    Configure();
                    return _inner.Execute();
                }

                private void Configure() => _inner!.TaskEnvironment = TaskEnvironment;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task TaskEnvironmentPassedToConstructor_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new InjectedTask(TaskEnvironment);
                    return inner.Execute();
                }
            }

            public class InjectedTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public InjectedTask(TaskEnvironment taskEnvironment) => TaskEnvironment = taskEnvironment;

                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute() => true;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConstructorTakingTaskEnvironmentNotUsed_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new InjectedTask();
                    return inner.Execute();
                }
            }

            public class InjectedTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public InjectedTask() => TaskEnvironment = null!;

                public InjectedTask(TaskEnvironment taskEnvironment) => TaskEnvironment = taskEnvironment;

                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute() => true;
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    [Fact]
    public async Task ConstructingTaskIsNotMultiThreadable_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var inner = new InnerTask { BuildEngine = BuildEngine };
                    return inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultiThreadableAttributeWithoutTaskEnvironment_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var inner = new InnerTask { BuildEngine = BuildEngine };
                    return inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultiThreadableAttributeWithTaskEnvironment_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                private readonly TaskEnvironment _taskEnvironment = new();

                public override bool Execute()
                {
                    var inner = new InnerTask { BuildEngine = BuildEngine };
                    return inner.Execute();
                }
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    [Fact]
    public async Task ConstructedTaskCannotReceiveTaskEnvironment_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var inner = new LegacyTask { BuildEngine = BuildEngine };
                    return inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConstructedTypeIsNotATask_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    var item = new TaskItem();
                    return item.ItemSpec.Length == 0;
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConstructedTaskInFieldInitializer_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                private readonly InnerTask _inner = new InnerTask();

                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute() => _inner.Execute();
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    [Fact]
    public async Task ConstructedTaskInFieldInitializerConfiguredLater_DoesNotProduceDiagnostic()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                private readonly InnerTask _inner = new InnerTask();

                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute()
                {
                    _inner.TaskEnvironment = TaskEnvironment;
                    return _inner.Execute();
                }
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConstructedTaskInStaticMethod_ProducesWarning()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute() => Run();

                private static bool Run() => new InnerTask().Execute();
            }
            """);

        diagnostics.Single().Id.ShouldBe(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);
    }

    private static async Task<Diagnostic[]> GetDiagnosticsAsync(string source)
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            $"{source}{System.Environment.NewLine}{InnerTasks}",
            new TaskEnvironmentPropagationAnalyzer());

        diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        diagnostics.Where(diagnostic => diagnostic.Id == "AD0001").ShouldBeEmpty();

        return diagnostics
            .Where(diagnostic => diagnostic.Id == DiagnosticIds.PropagateTaskEnvironmentToConstructedTask)
            .ToArray();
    }
}
