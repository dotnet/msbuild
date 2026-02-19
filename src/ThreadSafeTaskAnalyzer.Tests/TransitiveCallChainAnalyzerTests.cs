// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    #region Cross-Assembly IL Analysis Tests

    /// <summary>
    /// Creates a two-compilation setup:
    /// 1. A "library" assembly compiled from librarySource
    /// 2. A "task" assembly that references the library
    /// Runs the transitive analyzer on the task assembly.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> GetCrossAssemblyDiagnosticsAsync(
        string librarySource, string taskSource, string libraryAssemblyName = "TestLibrary")
    {
        // Step 1: Compile the library assembly
        var libSyntaxTree = CSharpSyntaxTree.ParseText(librarySource, path: "Library.cs");
        var stubSyntaxTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "Stubs.cs");

        var coreRefs = GetCoreReferences();

        var libCompilation = CSharpCompilation.Create(
            libraryAssemblyName,
            [libSyntaxTree, stubSyntaxTree],
            coreRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        // Emit to in-memory stream
        using var libStream = new System.IO.MemoryStream();
        var emitResult = libCompilation.Emit(libStream);
        emitResult.Success.ShouldBeTrue(
            $"Library compilation failed: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        libStream.Position = 0;

        // Write to temp file so PEReader can read it
        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{libraryAssemblyName}_{System.Guid.NewGuid():N}.dll");
        try
        {
            System.IO.File.WriteAllBytes(tempPath, libStream.ToArray());

            // Step 2: Compile the task assembly referencing the library
            var libReference = MetadataReference.CreateFromFile(tempPath);
            var taskSyntaxTree = CSharpSyntaxTree.ParseText(taskSource, path: "Task.cs");
            var taskStubTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "TaskStubs.cs");

            var taskCompilation = CSharpCompilation.Create(
                "TaskAssembly",
                [taskSyntaxTree, taskStubTree],
                coreRefs.Append(libReference).ToArray(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            // Run only the transitive analyzer
            var analyzer = new TransitiveCallChainAnalyzer();
            var compilationWithAnalyzers = taskCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public async Task CrossAssembly_LibraryCallingConsole_DetectedViaIL()
    {
        var librarySource = """
            using System;
            namespace MyLib
            {
                public static class LibHelper
                {
                    public static void UnsafeLog(string msg)
                    {
                        Console.WriteLine(msg);
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    LibHelper.UnsafeLog("hello");
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty("Should detect Console.WriteLine in referenced library");
        transitive[0].GetMessage().ShouldContain("Console");
    }

    [Fact]
    public async Task CrossAssembly_LibraryCallingPathGetFullPath_DetectedViaIL()
    {
        var librarySource = """
            using System.IO;
            namespace MyLib
            {
                public static class PathHelper
                {
                    public static string ResolvePath(string relativePath)
                    {
                        return Path.GetFullPath(relativePath);
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    PathHelper.ResolvePath("test.txt");
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty("Should detect Path.GetFullPath in referenced library");
        transitive[0].GetMessage().ShouldContain("GetFullPath");
    }

    [Fact]
    public async Task CrossAssembly_SafeType_NotFlagged()
    {
        // Simulate AbsolutePath calling Path.GetFullPath internally — should NOT be flagged
        var librarySource = """
            using System.IO;
            namespace Microsoft.Build.Framework
            {
                public struct AbsolutePath
                {
                    public string Value { get; }
                    public AbsolutePath(string path) { Value = path; }
                    public static implicit operator string(AbsolutePath p) => p.Value;

                    public AbsolutePath GetCanonicalForm()
                    {
                        // Internally calls Path.GetFullPath — this should be suppressed
                        return new AbsolutePath(Path.GetFullPath(Value));
                    }
                }

                public class TaskEnvironment
                {
                    public string ProjectDirectory { get; }
                    public string GetEnvironmentVariable(string name) => null;
                    public void SetEnvironmentVariable(string name, string value) { }
                    public System.Collections.IDictionary GetEnvironmentVariables() => null;
                    public AbsolutePath GetAbsolutePath(string path) => default;
                    public System.Diagnostics.ProcessStartInfo GetProcessStartInfo() => null;
                }

                public interface IBuildEngine { }
                public interface ITask
                {
                    IBuildEngine BuildEngine { get; set; }
                    bool Execute();
                }
                public interface IMultiThreadableTask : ITask
                {
                    TaskEnvironment TaskEnvironment { get; set; }
                }
                public interface ITaskItem
                {
                    string ItemSpec { get; set; }
                    string GetMetadata(string metadataName);
                }
            }
            namespace Microsoft.Build.Utilities
            {
                public abstract class Task : Microsoft.Build.Framework.ITask
                {
                    public Microsoft.Build.Framework.IBuildEngine BuildEngine { get; set; }
                    public abstract bool Execute();
                }
            }
            """;

        var taskSource = """
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var ap = new AbsolutePath("C:\\test");
                    var canonical = ap.GetCanonicalForm();
                    return true;
                }
            }
            """;

        // For safe-type tests, compile without the default stubs
        var libTree = CSharpSyntaxTree.ParseText(librarySource, path: "Lib.cs");
        var coreRefs = GetCoreReferences();

        var libCompilation = CSharpCompilation.Create(
            "FrameworkLib",
            [libTree],
            coreRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using var libStream = new System.IO.MemoryStream();
        var emitResult = libCompilation.Emit(libStream);
        emitResult.Success.ShouldBeTrue(
            $"Lib failed: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"FrameworkLib_{System.Guid.NewGuid():N}.dll");
        try
        {
            System.IO.File.WriteAllBytes(tempPath, libStream.ToArray());
            var libRef = MetadataReference.CreateFromFile(tempPath);

            var taskTree = CSharpSyntaxTree.ParseText(taskSource, path: "Task.cs");
            var taskCompilation = CSharpCompilation.Create(
                "TaskAssembly",
                [taskTree],
                coreRefs.Append(libRef).ToArray(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            var analyzer = new TransitiveCallChainAnalyzer();
            var compilationWithAnalyzers = taskCompilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

            var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
            // Path.GetFullPath inside AbsolutePath.GetCanonicalForm should be suppressed
            var getFullPathViolations = transitive.Where(d => d.GetMessage().Contains("GetFullPath")).ToArray();
            getFullPathViolations.ShouldBeEmpty(
                "Path.GetFullPath inside AbsolutePath (safe type) should not be flagged");
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public async Task CrossAssembly_DeepCallChain_DetectedViaIL()
    {
        var librarySource = """
            using System;
            using System.IO;
            namespace MyLib
            {
                public static class Level1
                {
                    public static void Start() { Level2.Middle(); }
                }
                public static class Level2
                {
                    public static void Middle() { Level3.End(); }
                }
                public static class Level3
                {
                    public static void End() { Environment.Exit(1); }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Level1.Start();
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty("Should detect Environment.Exit through 3-level deep chain in library");
        var msg = transitive[0].GetMessage();
        msg.ShouldContain("Exit");
        msg.ShouldContain("→");
    }

    [Fact]
    public async Task CrossAssembly_BclBoundary_NotTraversed()
    {
        // A library calling string.Format which internally calls... lots of stuff.
        // We should NOT traverse into System.Private.CoreLib.
        var librarySource = """
            namespace MyLib
            {
                public static class SafeHelper
                {
                    public static string Format(string template, object arg)
                    {
                        return string.Format(template, arg);
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    SafeHelper.Format("hello {0}", "world");
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        // string.Format is NOT a banned API, and we should not traverse INTO System.* assemblies
        transitive.ShouldBeEmpty("BCL internals should not be traversed");
    }

    [Fact]
    public async Task CrossAssembly_MixedSafeAndUnsafe_OnlyUnsafeFlagged()
    {
        var librarySource = """
            using System;
            using System.IO;
            namespace MyLib
            {
                public static class MixedHelper
                {
                    public static string SafeMethod(string a, string b)
                    {
                        return a + b;  // totally safe
                    }

                    public static void UnsafeMethod()
                    {
                        Environment.Exit(42);
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var result = MixedHelper.SafeMethod("a", "b");
                    MixedHelper.UnsafeMethod();
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);

        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        // Only UnsafeMethod → Environment.Exit should be flagged
        transitive.ShouldNotBeEmpty();
        transitive.All(d => d.GetMessage().Contains("UnsafeMethod")).ShouldBeTrue(
            "Only the unsafe method chain should be flagged");
    }

    [Fact]
    public async Task CrossAssembly_PropertyGetter_CallingUnsafeApi_Detected()
    {
        var librarySource = """
            using System;
            namespace MyLib
            {
                public class ConfigReader
                {
                    public string CurrentDir
                    {
                        get { return Environment.CurrentDirectory; }
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var reader = new ConfigReader();
                    var dir = reader.CurrentDir;
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);
        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty("Should detect Environment.CurrentDirectory in property getter via IL");
    }

    [Fact]
    public async Task CrossAssembly_ConstructorCallingUnsafeApi_Detected()
    {
        var librarySource = """
            using System;
            namespace MyLib
            {
                public class FileLogger
                {
                    public FileLogger(string logPath)
                    {
                        // Call a banned API directly
                        Environment.Exit(99);
                    }
                }
            }
            """;

        var taskSource = """
            using MyLib;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var logger = new FileLogger("log.txt");
                    return true;
                }
            }
            """;

        var diags = await GetCrossAssemblyDiagnosticsAsync(librarySource, taskSource);
        var transitive = diags.Where(d => d.Id == DiagnosticIds.TransitiveUnsafeCall).ToArray();
        transitive.ShouldNotBeEmpty("Should detect Environment.Exit in constructor via IL");
    }

    #endregion
}
