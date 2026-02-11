// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
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
    [Fact]
    public async Task HelperCallingConsole_TransitivelyFromTask_ProducesDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class Helper
            {
                public static void Log(string msg) { Console.WriteLine(msg); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Helper.Log("hello");
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain("Console.WriteLine");
        transitive[0].GetMessage().ShouldContain("Helper.Log");
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
    public async Task HelperCallingFileExists_WithoutAbsolutePath_ProducesDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System.IO;
            public class FileHelper
            {
                public static bool CheckFile(string path) { return File.Exists(path); }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    FileHelper.CheckFile("test.txt");
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain("File.Exists");
    }

    [Fact]
    public async Task HelperCallingEnvironmentGetVar_ProducesDiagnostic()
    {
        var diags = await GetAllDiagnosticsAsync("""
            using System;
            public class ConfigHelper
            {
                public static string GetConfig(string key)
                {
                    return Environment.GetEnvironmentVariable(key);
                }
            }

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var val = ConfigHelper.GetConfig("MY_VAR");
                    return true;
                }
            }
            """);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty();
        transitive[0].GetMessage().ShouldContain("GetEnvironmentVariable");
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
}
