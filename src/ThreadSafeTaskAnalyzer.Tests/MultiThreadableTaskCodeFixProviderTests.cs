// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
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

    private static DiagnosticResult Diag(string id) =>
        CSharpAnalyzerVerifier<MultiThreadableTaskAnalyzer, DefaultVerifier>.Diagnostic(id);

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
                .WithArguments("Environment.GetEnvironmentVariable(string)", "use TaskEnvironment.GetEnvironmentVariable instead")
        ).RunAsync();
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
                .WithArguments("Environment.SetEnvironmentVariable(string, string?)", "use TaskEnvironment.SetEnvironmentVariable instead")
        ).RunAsync();
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
                .WithArguments("Path.GetFullPath(string)", "use TaskEnvironment.GetAbsolutePath instead")
        ).RunAsync();
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
                .WithArguments("Environment.CurrentDirectory", "use TaskEnvironment.ProjectDirectory instead")
        ).RunAsync();
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
                .WithArguments("File.Exists(string?)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")
        ).RunAsync();
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
                .WithArguments("new FileInfo(...)", "wrap path argument with TaskEnvironment.GetAbsolutePath()")
        ).RunAsync();
    }
}
