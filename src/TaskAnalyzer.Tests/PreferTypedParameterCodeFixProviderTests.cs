// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class PreferTypedParameterCodeFixProviderTests
{
    private static CSharpCodeFixTest<PreferTypedParameterAnalyzer, PreferTypedParameterCodeFixProvider, DefaultVerifier> CreateFixTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<PreferTypedParameterAnalyzer, PreferTypedParameterCodeFixProvider, DefaultVerifier>
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

    private static DiagnosticResult Diag(string id) => id switch
    {
        DiagnosticIds.PreferTypedPathParameter => new DiagnosticResult(DiagnosticDescriptors.PreferTypedPathParameter),
        DiagnosticIds.PreferTypedTaskItem => new DiagnosticResult(DiagnosticDescriptors.PreferTypedTaskItem),
        _ => new DiagnosticResult(id, DiagnosticSeverity.Info),
    };

    [Fact]
    public async Task Fix_0006_NewAbsolutePath_RetypesPropertyAndRemovesConversion()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "";
                    public override bool Execute()
                    {
                        var abs = {|#0:new AbsolutePath(InputPath)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public AbsolutePath InputPath { get; set; } = default;
                    public override bool Execute()
                    {
                        var abs = InputPath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath")).RunAsync();
    }

    [Fact]
    public async Task Fix_0006_NewFileInfo_RetypesPropertyAndUsesTypedValue()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string FilePath { get; set; } = "";
                    public override bool Execute()
                    {
                        var fi = {|#0:new FileInfo(FilePath)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public FileInfo FilePath { get; set; } = null!;
                    public override bool Execute()
                    {
                        var fi = FilePath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("FilePath", "string", "FileInfo")).RunAsync();
    }

    [Fact]
    public async Task Fix_0007_IntParse_RetypesTaskItemAndUsesValue()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        int value = {|#0:int.Parse(Item.ItemSpec)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem<int> Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        int value = Item.Value;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedTaskItem).WithLocation(0)
                .WithArguments("Item", "ITaskItem", "int", "")).RunAsync();
    }

    [Fact]
    public async Task Fix_0007_ConvertToInt32_RetypesTaskItemAndUsesValue()
    {
        await CreateFixTest(
            testCode: """
                using System;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        int value = {|#0:Convert.ToInt32(Item.ItemSpec)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem<int> Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        int value = Item.Value;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedTaskItem).WithLocation(0)
                .WithArguments("Item", "ITaskItem", "int", "")).RunAsync();
    }

    [Fact]
    public async Task Fix_0007_GetAbsolutePath_RetypesTaskItemAndUsesValue()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public ITaskItem Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        var path = {|#0:TaskEnvironment.GetAbsolutePath(Item.ItemSpec)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public ITaskItem<AbsolutePath> Item { get; set; } = null!;
                    public override bool Execute()
                    {
                        var path = Item.Value;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedTaskItem).WithLocation(0)
                .WithArguments("Item", "ITaskItem", "AbsolutePath", "")).RunAsync();
    }

    [Fact]
    public async Task Fix_0007_ArrayForeach_RetypesArrayAndUsesValue()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem[] Items { get; set; } = null!;
                    public override bool Execute()
                    {
                        foreach (var item in Items)
                        {
                            var fi = {|#0:new FileInfo(item.ItemSpec)|};
                        }
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public ITaskItem<FileInfo>[] Items { get; set; } = null!;
                    public override bool Execute()
                    {
                        foreach (var item in Items)
                        {
                            var fi = item.Value;
                        }
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedTaskItem).WithLocation(0)
                .WithArguments("Items", "ITaskItem[]", "FileInfo", "[]")).RunAsync();
    }

    [Fact]
    public async Task Fix_0006_RewritesAllSitesForProperty()
    {
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "";
                    public override bool Execute()
                    {
                        var first = {|#0:new AbsolutePath(InputPath)|};
                        var second = {|#1:new AbsolutePath(InputPath)|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public AbsolutePath InputPath { get; set; } = default;
                    public override bool Execute()
                    {
                        var first = InputPath;
                        var second = InputPath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath"),
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(1)
                .WithArguments("InputPath", "string", "AbsolutePath")).RunAsync();
    }
}
