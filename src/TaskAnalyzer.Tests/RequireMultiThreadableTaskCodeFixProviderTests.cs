// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="RequireMultiThreadableTaskCodeFixProvider"/>, the code fix for MSBuildTask0012.
/// The rule only reports when it is opted into, so every test supplies a .globalconfig setting the scope.
/// </summary>
public class RequireMultiThreadableTaskCodeFixProviderTests
{
    private const string GlobalConfig = """
        is_global = true
        msbuild_task_analyzer.scope = require_multithreadable
        """;

    private static CSharpCodeFixTest<RequireMultiThreadableTaskAnalyzer, RequireMultiThreadableTaskCodeFixProvider, DefaultVerifier> CreateFixTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<RequireMultiThreadableTaskAnalyzer, RequireMultiThreadableTaskCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static DiagnosticResult Diag(string taskName) =>
        new DiagnosticResult(DiagnosticDescriptors.RequireMultiThreadableTask).WithLocation(0).WithArguments(taskName);

    [Fact]
    public async Task Fix_AddsAttributeInterfaceAndProperty()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                public class {|#0:MyTask|} : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute() => true;
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

                    public override bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_TaskAlreadyImplementingInterface_AddsAttributeOnly()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                public class {|#0:MyTask|} : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
                    public override bool Execute() => true;
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
                    public override bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_TaskDerivingFromMultiThreadableBase_AddsAttributeOnly()
    {
        // The base already provides the interface and the property; only the leaf's opt-in is missing,
        // because the attribute is not inherited.
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public abstract class MultiThreadableTaskBase : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
                }

                public class {|#0:MyTask|} : MultiThreadableTaskBase
                {
                    public override bool Execute() => true;
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public abstract class MultiThreadableTaskBase : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
                }

                [MSBuildMultiThreadableTask]
                public class MyTask : MultiThreadableTaskBase
                {
                    public override bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_TaskWithoutFrameworkUsing_QualifiesAddedTypes()
    {
        await CreateFixTest(
            testCode: """
                public class {|#0:MyTask|} : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute() => true;
                }
                """,
            fixedCode: """
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
                {
                    public Microsoft.Build.Framework.TaskEnvironment TaskEnvironment { get; set; } = Microsoft.Build.Framework.TaskEnvironment.Fallback;

                    public override bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_TaskWithConflictingTaskEnvironmentMember_AddsAttributeOnly()
    {
        // Declaring the interface would not compile against an unrelated member of the same name, so only the
        // attribute is applied — which is on its own a valid opt-in as far as the engine's routing is concerned.
        await CreateFixTest(
            testCode: """
                public class {|#0:MyTask|} : Microsoft.Build.Utilities.Task
                {
                    public string TaskEnvironment { get; set; } = "";
                    public override bool Execute() => true;
                }
                """,
            fixedCode: """
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string TaskEnvironment { get; set; } = "";
                    public override bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }

    [Fact]
    public async Task Fix_TaskImplementingITaskDirectly_AddsAttributeInterfaceAndProperty()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                public class {|#0:MyTask|} : ITask
                {
                    public IBuildEngine BuildEngine { get; set; }
                    public bool Execute() => true;
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;

                [MSBuildMultiThreadableTask]
                public class MyTask : ITask, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
                    public IBuildEngine BuildEngine { get; set; }
                    public bool Execute() => true;
                }
                """,
            Diag("MyTask")).RunAsync();
    }
}
