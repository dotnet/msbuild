using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
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
    [InlineData("using System;", "Console.WriteLine(\"test\");", "Console.WriteLine")]
    [InlineData("using System.IO;", "File.Exists(\"test.txt\");", "File.Exists")]
    [InlineData("using System;", "Environment.GetEnvironmentVariable(\"KEY\");", "GetEnvironmentVariable")]
    public async Task HelperCallingBannedApi_TransitivelyFromTask_ProducesDiagnostic(
        string usingDirective, string helperBody, string expectedApiName)
    {
        var source = $$"""
            {{usingDirective}}
            using Microsoft.Build.Framework;
            public class TestHelper
            {
                public static void DoWork() { {{helperBody}} }
            }

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    TestHelper.DoWork();
                    return true;
                }
            }
            """;

        var diags = await GetAllDiagnosticsAsync(source);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain(expectedApiName);
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        var msg = transitive[0].GetMessage();
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall);
        transitive.ShouldBeEmpty();

        var direct = diags.Where(d => d.Id == DiagnosticIds.CriticalError);
        direct.ShouldNotBeEmpty();
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall);
        transitive.ShouldBeEmpty();
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
        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain("Console.WriteLine");
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain("Console.Write");
    }

    [Fact]
    public async Task MultipleViolationsInChain_AllReported()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;
            public class UnsafeHelper
            {
                public static void DoStuff()
                {
                    Console.WriteLine("log");
                    Environment.Exit(1);
                    File.Exists("test.txt");
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    UnsafeHelper.DoStuff();
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.Length.ShouldBeGreaterThanOrEqualTo(3);
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        var msg = transitive[0].GetMessage();
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

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scope_Default_PlainTask_GetsAlwaysApplicableTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            public static class Helper
            {
                public static void Run() => Environment.Exit(1);
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

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Scope_Default_PlainTask_GetsPotentialIssueTransitiveDiagnostic()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System.Reflection;
            public static class Helper
            {
                public static void Run() => Assembly.LoadFrom("helper.dll");
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

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
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

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Scope_Default_MultiThreadableAttribute_OptsTaskIntoTransitiveAnalysis()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            using Microsoft.Build.Framework;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            [MSBuildMultiThreadableTask]
            public class MtTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Scope_Default_AnalyzedAttribute_OptsTaskIntoTransitiveAnalysis()
    {
        var diags = await GetAllDiagnosticsWithDefaultScopeAsync("""
            using System;
            using Microsoft.Build.Framework;
            public static class Helper
            {
                public static void Run() => Environment.GetEnvironmentVariable("KEY");
            }
            [MSBuildMultiThreadableTaskAnalyzed]
            public class MtTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Run();
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
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

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Scope_GlobalConfig_All_AnalyzesPlainTaskTransitively()
    {
        var test = new CSharpAnalyzerTest<TransitiveCallChainAnalyzer, DefaultVerifier>
        {
            TestCode = """
                using System;
                public static class Helper
                {
                    public static void Run() => Environment.GetEnvironmentVariable("KEY");
                }
                public class PlainTask : Microsoft.Build.Utilities.Task
                {
                    public override bool {|#0:Execute|}()
                    {
                        Helper.Run();
                        return true;
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true
            msbuild_task_analyzer.scope = all
            """));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DiagnosticIds.TransitiveUnsafeCall, DiagnosticSeverity.Warning).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task Diagnostic_IsReportedAtUnsafeCallSite_NotAtTaskEntryPoint()
    {
        var source = """
            using System;
            public class TestHelper
            {
                public static void DoWork() { Environment.Exit(1); }
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

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();

        // Primary location is the unsafe call itself, so a suppression next to it is honored.
        var span = transitive[0].Location.SourceSpan;
        source.Substring(span.Start, span.Length).ShouldBe("Environment.Exit(1)");

        // The task entry point remains reachable as an additional location.
        transitive[0].AdditionalLocations.Count.ShouldBe(1);
        var taskSpan = transitive[0].AdditionalLocations[0].SourceSpan;
        source.Substring(taskSpan.Start, taskSpan.Length).ShouldBe("Execute");
    }

    [Fact]
    public async Task PragmaDisableAtUnsafeCallSite_SuppressesDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System.Diagnostics;
            public class ProcessService
            {
                public static void KillProcessTree(Process process)
                {
            #pragma warning disable MSBuildTask0005
                    process.Kill(entireProcessTree: true);
            #pragma warning restore MSBuildTask0005
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ProcessService.KillProcessTree(new Process());
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldBeEmpty();
    }

    [Fact]
    public async Task SuppressMessageAttributeOnHelperMethod_SuppressesDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System.Diagnostics;
            public class ProcessService
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("MSBuild.TaskAuthoring", "MSBuildTask0005", Justification = "Only kills processes this task started.")]
                public static void KillProcessTree(Process process)
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ProcessService.KillProcessTree(new Process());
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ShouldBeEmpty();
    }

    [Fact]
    public async Task PragmaDisableAtOneCallSite_StillReportsOtherCallSiteOfSameApi()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System.Diagnostics;
            public class ProcessService
            {
                public static void ReviewedKill(Process process)
                {
            #pragma warning disable MSBuildTask0005
                    process.Kill(entireProcessTree: true);
            #pragma warning restore MSBuildTask0005
                }

                public static void UnreviewedKill(Process process)
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ProcessService.ReviewedKill(new Process());
                    ProcessService.UnreviewedKill(new Process());
                    return true;
                }
            }
            """);

        // Suppressing one reviewed call must not blind the analyzer to the other call to the same API.
        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.Length.ShouldBe(1);
        transitive[0].GetMessage().ShouldContain("UnreviewedKill");
    }

    [Fact]
    public async Task PragmaDisableAtCallSite_DoesNotHideUnrelatedTransitiveViolations()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            using System.Diagnostics;
            public class ProcessService
            {
                public static void Run(Process process)
                {
            #pragma warning disable MSBuildTask0005
                    process.Kill(entireProcessTree: true);
            #pragma warning restore MSBuildTask0005
                    Environment.Exit(1);
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ProcessService.Run(new Process());
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.Length.ShouldBe(1);
        transitive[0].GetMessage().ShouldContain("Environment.Exit");
    }

    [Fact]
    public async Task DistinctCallSitesOfSameApi_EachReportedSeparately()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class UnsafeHelper
            {
                public static void First() { Environment.Exit(1); }
                public static void Second() { Environment.Exit(2); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    UnsafeHelper.First();
                    UnsafeHelper.Second();
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.Length.ShouldBe(2);
        transitive.Select(d => d.Location.SourceSpan).Distinct().Count().ShouldBe(2);
    }
}