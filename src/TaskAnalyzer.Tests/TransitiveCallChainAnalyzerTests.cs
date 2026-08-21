// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="TransitiveCallChainAnalyzer"/> — verifies that unsafe API usage
/// reachable through helper method calls is detected and reported with call chains.
/// </summary>
public class TransitiveCallChainAnalyzerTests
{
    [Theory]
    [InlineData("using System;", "Console.WriteLine(\"test\");", "Console.WriteLine", DiagnosticIds.CriticalError, DiagnosticSeverity.Error)]
    [InlineData("using System.IO;", "File.Exists(\"test.txt\");", "File.Exists", DiagnosticIds.FilePathRequiresAbsolute, DiagnosticSeverity.Warning)]
    [InlineData("using System;", "Environment.GetEnvironmentVariable(\"KEY\");", "GetEnvironmentVariable", DiagnosticIds.TaskEnvironmentRequired, DiagnosticSeverity.Warning)]
    [InlineData("using System.Reflection;", "Assembly.Load(\"Test\");", "Assembly.Load", DiagnosticIds.PotentialIssue, DiagnosticSeverity.Warning)]
    public async Task HelperCallingBannedApi_ReportsUnderlyingDiagnostic(
        string usingDirective,
        string helperBody,
        string expectedApiName,
        string expectedDiagnosticId,
        DiagnosticSeverity expectedSeverity)
    {
        var source = $$"""
            {{usingDirective}}
            public class TestHelper
            {
                public static void DoWork() { {{helperBody}} }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    TestHelper.DoWork();
                    return true;
                }
            }
            """;

        var diags = await GetAllDiagnosticsAsync(source);

        var transitive = diags.Where(d =>
            d.Id == expectedDiagnosticId &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();

        transitive.Severity.ShouldBe(expectedSeverity);
        transitive.GetMessage().ShouldContain(expectedApiName);
        transitive.GetMessage().ShouldContain("MyTask.Execute");
        transitive.Properties[DiagnosticIds.IsTransitiveProperty].ShouldBe(bool.TrueString);
        transitive.Location.SourceTree!.GetText().ToString(transitive.Location.SourceSpan).ShouldContain(expectedApiName);
        transitive.AdditionalLocations.ShouldHaveSingleItem()
            .SourceTree!.GetText().ToString(transitive.AdditionalLocations[0].SourceSpan).ShouldBe("Execute");
    }

    [Fact]
    public async Task TwoLevelChain_HelperCallingHelperCallingBannedApi()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class InnerHelper
            {
                public static void DoExit() { Environment.Exit(1); }
            }
            public class OuterHelper
            {
                public static void Process() { InnerHelper.DoExit(); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    OuterHelper.Process();
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d =>
            d.Id == DiagnosticIds.CriticalError &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();
        var msg = transitive.GetMessage();
        msg.ShouldContain("Environment.Exit");
        // Chain should show: MyTask.Execute → OuterHelper.Process → InnerHelper.DoExit → Environment.Exit
        msg.ShouldContain("OuterHelper.Process");
        msg.ShouldContain("InnerHelper.DoExit");
    }

    [Fact]
    public async Task DirectCallInTask_NotReportedAsTransitive()
    {
        // Direct calls within the task should only produce direct diagnostics, not transitive
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.WriteLine("direct");
                    return true;
                }
            }
            """);

        var direct = diags.Where(d => d.Id == DiagnosticIds.CriticalError).ShouldHaveSingleItem();
        direct.GetMessage().ShouldNotContain("reachable from task method");
    }

    [Fact]
    public async Task SafeHelper_NoTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            public class SafeHelper
            {
                public static int Add(int a, int b) => a + b;
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var result = SafeHelper.Add(1, 2);
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id is
            DiagnosticIds.CriticalError or
            DiagnosticIds.TaskEnvironmentRequired or
            DiagnosticIds.FilePathRequiresAbsolute or
            DiagnosticIds.PotentialIssue).ShouldBeEmpty();
    }

    [Fact]
    public async Task RecursiveCallChain_DoesNotStackOverflow()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class RecursiveHelper
            {
                public static void A() { B(); }
                public static void B() { A(); Console.WriteLine("recurse"); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    RecursiveHelper.A();
                    return true;
                }
            }
            """);

        // Should still detect the violation without infinite loop
        var transitive = diags.Where(d =>
            d.Id == DiagnosticIds.CriticalError &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();
        transitive.GetMessage().ShouldContain("Console.WriteLine");
    }

    [Fact]
    public async Task InstanceMethodHelper_TransitivelyDetected()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class Logger
            {
                public void Write(string msg) { Console.Write(msg); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var logger = new Logger();
                    logger.Write("hello");
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d =>
            d.Id == DiagnosticIds.CriticalError &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();
        transitive.GetMessage().ShouldContain("Console.Write");
    }

    [Fact]
    public async Task MultipleViolationsInChain_AllReported()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            using System.IO;
            public class UnsafeHelper
            {
                public static void DoStuff()
                {
                    Console.WriteLine("log");
                    Environment.Exit(1);
                    File.Exists("test.txt");
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    UnsafeHelper.DoStuff();
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.GetMessage().Contains("reachable from task method")).ToArray();
        transitive.Length.ShouldBe(3);
        transitive.Count(d => d.Id == DiagnosticIds.CriticalError).ShouldBe(2);
        transitive.Count(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).ShouldBe(1);
    }

    [Fact]
    public async Task ChainMessageFormat_ContainsArrowSeparatedMethods()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class A
            {
                public static void Step1() { B.Step2(); }
            }
            public class B
            {
                public static void Step2() { Environment.Exit(1); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    A.Step1();
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d =>
            d.Id == DiagnosticIds.CriticalError &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();
        var msg = transitive.GetMessage();
        // Should contain arrow-separated chain
        msg.ShouldContain("→");
        msg.ShouldContain("A.Step1");
        msg.ShouldContain("B.Step2");
    }

    [Fact]
    public async Task Scope_Default_PlainTask_DoesNotGetTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class PlainTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scope_Default_MultiThreadableTask_GetsTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            using Microsoft.Build.Framework;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class MtTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).ShouldHaveSingleItem()
            .GetMessage().ShouldContain("reachable from task method");
    }

    [Fact]
    public async Task Scope_All_PlainTask_GetsTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithScopeAsync("""
            using System;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class PlainTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """, SharedAnalyzerHelpers.ScopeAll);

        diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).ShouldHaveSingleItem()
            .GetMessage().ShouldContain("reachable from task method");
    }

    [Theory]
    [InlineData("Console.WriteLine(\"test\");", DiagnosticIds.CriticalError, DiagnosticSeverity.Error)]
    [InlineData("System.Reflection.Assembly.Load(\"Test\");", DiagnosticIds.PotentialIssue, DiagnosticSeverity.Warning)]
    public async Task Scope_Default_PlainTask_GetsRulesThatApplyToAllTasks(
        string helperBody,
        string expectedDiagnosticId,
        DiagnosticSeverity expectedSeverity)
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync($$"""
            using System;
            public static class Helper
            {
                public static void Run() { {{helperBody}} }
            }
            public class PlainTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        var diagnostic = diags.Where(d =>
            d.Id == expectedDiagnosticId &&
            d.GetMessage().Contains("reachable from task method")).ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(expectedSeverity);
    }

    [Fact]
    public async Task UnderlyingDiagnosticConfiguration_SuppressesTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDiagnosticActionAsync("""
            using System;
            using Microsoft.Build.Framework;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class MtTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """, DiagnosticIds.TaskEnvironmentRequired, ReportDiagnostic.Suppress);

        diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectlyAnalyzedHelper_DoesNotGetDuplicateTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            using Microsoft.Build.Framework;
            [MSBuildMultiThreadableTaskAnalyzed]
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class MtTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        var diagnostic = diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).ShouldHaveSingleItem();
        diagnostic.Properties.ContainsKey(DiagnosticIds.IsTransitiveProperty).ShouldBeFalse();
    }

    [Fact]
    public async Task TransitiveDiagnostic_DoesNotOfferCodeFix()
    {
        const string source = """
            using System;
            using Microsoft.Build.Framework;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            public class MtTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """;

        var diagnostics = await GetAllDiagnosticsWithDefaultScopeAsync(source);
        var diagnostic = diagnostics.Where(d =>
            d.Id == DiagnosticIds.TaskEnvironmentRequired &&
            d.Properties.ContainsKey(DiagnosticIds.IsTransitiveProperty)).ShouldHaveSingleItem();

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await new MultiThreadableTaskCodeFixProvider().RegisterCodeFixesAsync(context);

        actions.ShouldBeEmpty();
    }
}