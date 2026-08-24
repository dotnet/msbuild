// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="TaskEnvironmentPropagationCodeFixProvider"/>, which adds the missing
/// <c>TaskEnvironment</c> entry to the object initializer of a task constructed inside another task.
/// </summary>
public class TaskEnvironmentPropagationCodeFixProviderTests
{
    private const string InnerTask = """

        public class InnerTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public Microsoft.Build.Framework.TaskEnvironment TaskEnvironment { get; set; } = null!;
            public override bool Execute() => true;
        }
        """;

    private static CSharpCodeFixTest<TaskEnvironmentPropagationAnalyzer, TaskEnvironmentPropagationCodeFixProvider, DefaultVerifier> CreateFixTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<TaskEnvironmentPropagationAnalyzer, TaskEnvironmentPropagationCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode + InnerTask,
            FixedCode = fixedCode + InnerTask,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static DiagnosticResult Diag() =>
        new DiagnosticResult(DiagnosticDescriptors.PropagateTaskEnvironmentToConstructedTask);

    [Fact]
    public async Task Fix_AddsEntryToExistingInitializer()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;

                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = null!;

                    public override bool Execute()
                    {
                        var inner = {|#0:new InnerTask { BuildEngine = BuildEngine }|};
                        return inner.Execute();
                    }
                }
                """,
            fixedCode: """
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
                """,
            Diag().WithLocation(0).WithArguments("InnerTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_AddsInitializerWhenMissing()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;

                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = null!;

                    public override bool Execute()
                    {
                        var inner = {|#0:new InnerTask()|};
                        return inner.Execute();
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = null!;

                    public override bool Execute()
                    {
                        var inner = new InnerTask() { TaskEnvironment = TaskEnvironment };
                        return inner.Execute();
                    }
                }
                """,
            Diag().WithLocation(0).WithArguments("InnerTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_UsesTaskEnvironmentFieldOfConstructingTask()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    private readonly TaskEnvironment _taskEnvironment = new();

                    public override bool Execute()
                    {
                        var inner = {|#0:new InnerTask { BuildEngine = BuildEngine }|};
                        return inner.Execute();
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    private readonly TaskEnvironment _taskEnvironment = new();

                    public override bool Execute()
                    {
                        var inner = new InnerTask { BuildEngine = BuildEngine, TaskEnvironment = _taskEnvironment };
                        return inner.Execute();
                    }
                }
                """,
            Diag().WithLocation(0).WithArguments("InnerTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_IsNotOfferedInStaticContext()
    {
        const string Source = """
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;

                public override bool Execute() => Run();

                private static bool Run() => {|#0:new InnerTask()|}.Execute();
            }
            """;

        await CreateFixTest(
            testCode: Source,
            fixedCode: Source,
            Diag().WithLocation(0).WithArguments("InnerTask")).RunAsync();
    }
}
