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
/// Tests for <see cref="MultiThreadableTaskCodeFixProvider"/>.
/// Uses CSharpCodeFixTest for verifying code transformations.
/// Arguments are provided with nullable annotations matching .NET 8+ BCL.
/// </summary>
public class MultiThreadableTaskCodeFixProviderTests
{
    private static CSharpCodeFixTest<MultiThreadableTaskAnalyzer, MultiThreadableTaskCodeFixProvider, DefaultVerifier> CreateFixTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<MultiThreadableTaskAnalyzer, MultiThreadableTaskCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    /// <summary>
    /// Creates a DiagnosticResult for the given diagnostic ID.
    /// Uses the Warning-severity descriptor since code fix tests use IMultiThreadableTask.
    /// </summary>
    private static DiagnosticResult Diag(string id) => id switch
    {
        DiagnosticIds.CriticalError => new DiagnosticResult(DiagnosticDescriptors.CriticalError),
        DiagnosticIds.TaskEnvironmentRequired => new DiagnosticResult(DiagnosticDescriptors.TaskEnvironmentRequired),
        DiagnosticIds.FilePathRequiresAbsolute => new DiagnosticResult(DiagnosticDescriptors.FilePathRequiresAbsolute),
        DiagnosticIds.PotentialIssue => new DiagnosticResult(DiagnosticDescriptors.PotentialIssue),
        DiagnosticIds.TransitiveUnsafeCall => new DiagnosticResult(DiagnosticDescriptors.TransitiveUnsafeCall),
        _ => new DiagnosticResult(id, DiagnosticSeverity.Warning),
    };

    [Fact]
    public async Task Fix_GetEnvironmentVariable()
    {
        await CreateFixTest(
            testCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var val = {|#0:Environment.GetEnvironmentVariable("PATH")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var val = TaskEnvironment.GetEnvironmentVariable("PATH");
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Environment.GetEnvironmentVariable(string)", "use TaskEnvironment.GetEnvironmentVariable instead")).RunAsync();
    }

    [Fact]
    public async Task Fix_SetEnvironmentVariable()
    {
        await CreateFixTest(
            testCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        {|#0:Environment.SetEnvironmentVariable("KEY", "VALUE")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        TaskEnvironment.SetEnvironmentVariable("KEY", "VALUE");
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable(string, string?)", "use TaskEnvironment.SetEnvironmentVariable instead")).RunAsync();
    }

    [Fact]
    public async Task Fix_PathGetFullPath()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var p = {|#0:Path.GetFullPath("relative")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var p = TaskEnvironment.GetAbsolutePath("relative");
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Path.GetFullPath(string)", "use TaskEnvironment.GetAbsolutePath instead")).RunAsync();
    }

    [Fact]
    public async Task Fix_EnvironmentCurrentDirectory()
    {
        await CreateFixTest(
            testCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var dir = {|#0:Environment.CurrentDirectory|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var dir = TaskEnvironment.ProjectDirectory;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Environment.CurrentDirectory", "use TaskEnvironment.ProjectDirectory instead")).RunAsync();
    }

    [Fact]
    public async Task Fix_FileExists_WrapsWithGetAbsolutePath()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        {|#0:File.Exists("foo.txt")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        File.Exists(TaskEnvironment.GetAbsolutePath("foo.txt"));
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.Exists(string?)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_NewFileInfo_WrapsWithGetAbsolutePath()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var fi = {|#0:new FileInfo("file.txt")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        var fi = new FileInfo(TaskEnvironment.GetAbsolutePath("file.txt"));
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("new FileInfo(...)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_CallNestedInObjectCreation_WrapsFlaggedCallArgument()
    {
        // The flagged call is an argument of an outer object creation. The wrap must land on the flagged
        // call's own path argument, not on the outer call's argument (which is a Stream, not a string).
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string OutputPath { get; set; }
                    public override bool Execute()
                    {
                        using (var writer = new StreamWriter({|#0:File.Create(OutputPath)|}))
                        {
                            writer.Write("x");
                        }
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string OutputPath { get; set; }
                    public override bool Execute()
                    {
                        using (var writer = new StreamWriter(File.Create(TaskEnvironment.GetAbsolutePath(OutputPath))))
                        {
                            writer.Write("x");
                        }
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.Create(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_CallNestedInInvocation_WrapsFlaggedCallArgument()
    {
        await CreateFixTest(
            testCode: """
                using System.Xml.Linq;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputPath { get; set; }
                    public override bool Execute()
                    {
                        Consume({|#0:XDocument.Load(InputPath)|});
                        return true;
                    }
                    private static void Consume(XDocument doc)
                    {
                    }
                }
                """,
            fixedCode: """
                using System.Xml.Linq;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputPath { get; set; }
                    public override bool Execute()
                    {
                        Consume(XDocument.Load(TaskEnvironment.GetAbsolutePath(InputPath)));
                        return true;
                    }
                    private static void Consume(XDocument doc)
                    {
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("XDocument.Load(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_EnvironmentCallNestedInInvocation_ReplacesFlaggedCall()
    {
        // MSBuildTask0002 nested as an argument: the replacement must land on the flagged inner call,
        // not on the enclosing invocation.
        await CreateFixTest(
            testCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        Consume({|#0:Environment.GetEnvironmentVariable("PATH")|});
                        return true;
                    }
                    private static void Consume(string value)
                    {
                    }
                }
                """,
            fixedCode: """
                using System;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        Consume(TaskEnvironment.GetEnvironmentVariable("PATH"));
                        return true;
                    }
                    private static void Consume(string value)
                    {
                    }
                }
                """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Environment.GetEnvironmentVariable(string)", "use TaskEnvironment.GetEnvironmentVariable instead")).RunAsync();
    }

    [Fact]
    public async Task Fix_NonPathStringArgumentIsNotWrapped()
    {
        // The first argument is a search pattern, not a path: the wrap must land on the flagged path parameter.
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputDir { get; set; }
                    public override bool Execute()
                    {
                        {|#0:Directory.GetFiles(searchPattern: "*.cs", path: InputDir)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputDir { get; set; }
                    public override bool Execute()
                    {
                        Directory.GetFiles(searchPattern: "*.cs", path: TaskEnvironment.GetAbsolutePath(InputDir));
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("Directory.GetFiles(string, string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_StaticMethod_NoFixOffered()
    {
        // TaskEnvironment is an instance property, so a wrap emitted in a static method would not compile
        // (CS0120). The diagnostic is still reported, but no fix is offered.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; }
                public override bool Execute()
                {
                    ReadStatic(InputPath);
                    return true;
                }
                private static string ReadStatic(string path) => {|#0:File.ReadAllText(path)|};
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.ReadAllText(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_StaticLocalFunction_NoFixOffered()
    {
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; }
                public override bool Execute()
                {
                    Read(InputPath);
                    return true;
                    static string Read(string path) => {|#0:File.ReadAllText(path)|};
                }
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.ReadAllText(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_StaticLambda_NoFixOffered()
    {
        await CreateNoFixTest(
            """
            using System;
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; }
                public override bool Execute()
                {
                    Func<string, bool> exists = static path => {|#0:File.Exists(path)|};
                    return exists(InputPath);
                }
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.Exists(string?)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_TaskWithoutTaskEnvironmentMember_NoFixOffered()
    {
        // Under the default scope every ITask is analyzed, including plain tasks that have no
        // TaskEnvironment member to reference (CS0103).
        await CreateNoFixTest(
            """
            using System.IO;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; }
                public override bool Execute()
                {
                    return {|#0:File.Exists(InputPath)|};
                }
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.Exists(string?)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_LambdaInInstanceMethod_StillFixed()
    {
        // A non-static lambda inside an instance method can still reach the instance TaskEnvironment property.
        await CreateFixTest(
            testCode: """
                using System;
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputPath { get; set; }
                    public override bool Execute()
                    {
                        Func<bool> read = () => {|#0:File.Exists(InputPath)|};
                        return read();
                    }
                }
                """,
            fixedCode: """
                using System;
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputPath { get; set; }
                    public override bool Execute()
                    {
                        Func<bool> read = () => File.Exists(TaskEnvironment.GetAbsolutePath(InputPath));
                        return read();
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.Exists(string?)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    [Fact]
    public async Task Fix_GetEnvironmentVariableInStaticMethod_NoFixOffered()
    {
        // TaskEnvironment.GetEnvironmentVariable() is an instance call as well, so the MSBuildTask0002 fix
        // must be withheld in a static context for the same reason.
        await CreateNoFixTest(
            """
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute() => Read() is not null;
                private static string Read() => {|#0:Environment.GetEnvironmentVariable("PATH")|};
            }
            """,
            Diag(DiagnosticIds.TaskEnvironmentRequired).WithLocation(0)
                .WithArguments("Environment.GetEnvironmentVariable(string)", "use TaskEnvironment.GetEnvironmentVariable instead"));
    }

    [Fact]
    public async Task FixAll_NestedAndStaticCalls_FixesEachFlaggedCallInPlace()
    {
        // Mixed shapes in one document, applied in bulk (the "dotnet format analyzers" workflow): each wrap
        // must land on the flagged call's own path argument, and the static helper must be left alone.
        const string testCode = """
            using System.IO;
            using System.Xml.Linq;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; }
                public string OutputPath { get; set; }
                public override bool Execute()
                {
                    using (var writer = new StreamWriter({|#0:File.Create(OutputPath)|}))
                    {
                        writer.Write("x");
                    }
                    using (var reader = new StreamReader({|#1:File.OpenRead(InputPath)|}))
                    {
                        reader.ReadToEnd();
                    }
                    Consume({|#2:XDocument.Load(InputPath)|});
                    ReadStatic(InputPath);
                    return true;
                }
                private static void Consume(XDocument doc)
                {
                }
                private static string ReadStatic(string path) => {|#3:File.ReadAllText(path)|};
            }
            """;
        const string fixedCode = """
            using System.IO;
            using System.Xml.Linq;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; }
                public string OutputPath { get; set; }
                public override bool Execute()
                {
                    using (var writer = new StreamWriter(File.Create(TaskEnvironment.GetAbsolutePath(OutputPath))))
                    {
                        writer.Write("x");
                    }
                    using (var reader = new StreamReader(File.OpenRead(TaskEnvironment.GetAbsolutePath(InputPath))))
                    {
                        reader.ReadToEnd();
                    }
                    Consume(XDocument.Load(TaskEnvironment.GetAbsolutePath(InputPath)));
                    ReadStatic(InputPath);
                    return true;
                }
                private static void Consume(XDocument doc)
                {
                }
                private static string ReadStatic(string path) => {|#3:File.ReadAllText(path)|};
            }
            """;

        var test = new CSharpCodeFixTest<MultiThreadableTaskAnalyzer, MultiThreadableTaskCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));

        const string Hint = "wrap path argument with TaskEnvironment.GetAbsolutePath()";
        var staticDiagnostic = Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(3)
            .WithArguments("File.ReadAllText(string)", Hint);

        test.TestState.ExpectedDiagnostics.AddRange(
        [
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0).WithArguments("File.Create(string)", Hint),
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(1).WithArguments("File.OpenRead(string)", Hint),
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(2).WithArguments("XDocument.Load(string)", Hint),
            staticDiagnostic,
        ]);
        test.FixedState.ExpectedDiagnostics.Add(staticDiagnostic);

        await test.RunAsync();
    }

    [Fact]
    public async Task Fix_InstancePropertyInitializer_NoFixOffered()
    {
        // A property initializer runs before `this` is usable, so TaskEnvironment is unreachable there (CS0236).
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string Text { get; set; } = {|#0:File.ReadAllText("file.txt")|};
                public override bool Execute() => true;
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.ReadAllText(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_ConstructorInitializer_NoFixOffered()
    {
        // A constructor initializer runs before the instance exists, so TaskEnvironment is unreachable there.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public MyTask() : this({|#0:File.ReadAllText("file.txt")|}) { }
                public MyTask(string text) { }
                public override bool Execute() => true;
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.ReadAllText(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_PrimaryConstructorBaseArguments_NoFixOffered()
    {
        // A primary constructor's base argument list is evaluated before the instance exists, so
        // TaskEnvironment is unreachable there.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            public class BaseTask : Microsoft.Build.Utilities.Task
            {
                public BaseTask(string text) { }
                public override bool Execute() => true;
            }
            public class MyTask(string path) : BaseTask({|#0:File.ReadAllText(path)|}), IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
            }
            """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("File.ReadAllText(string)", "wrap path argument with TaskEnvironment.GetAbsolutePath()"));
    }

    [Fact]
    public async Task Fix_ImplicitObjectCreation_WrapsPathArgument()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        FileInfo fi = {|#0:new("file.txt")|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public override bool Execute()
                    {
                        FileInfo fi = new(TaskEnvironment.GetAbsolutePath("file.txt"));
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.FilePathRequiresAbsolute).WithLocation(0)
                .WithArguments("new FileInfo(...)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")).RunAsync();
    }

    /// <summary>
    /// Builds a code-fix test where the diagnostic is expected but no fix is offered: the fixed source is
    /// identical to the test source, so applying any offered fix would fail the comparison.
    /// </summary>
    private static async Task CreateNoFixTest(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<MultiThreadableTaskAnalyzer, MultiThreadableTaskCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("Test.cs", code));
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("Test.cs", code));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.TestState.ExpectedDiagnostics.AddRange(expected);
        test.FixedState.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
