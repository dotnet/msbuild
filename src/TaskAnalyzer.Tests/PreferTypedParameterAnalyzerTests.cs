// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="PreferTypedParameterAnalyzer"/> covering MSBuildTask0006 and MSBuildTask0007.
/// </summary>
public class PreferTypedParameterAnalyzerTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0006: Prefer typed path parameters
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NewAbsolutePath_FromStringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InputPath);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("InputPath");
        diags[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task NewFileInfo_FromStringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string FilePath { get; set; } = "";
                public override bool Execute()
                {
                    var fi = new FileInfo(FilePath);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task NewDirectoryInfo_FromStringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string DirPath { get; set; } = "";
                public override bool Execute()
                {
                    var di = new DirectoryInfo(DirPath);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags[0].GetMessage().ShouldContain("DirectoryInfo");
    }

    [Fact]
    public async Task TaskEnvironmentGetAbsolutePath_FromStringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Framework.IMultiThreadableTask
            {
                public IBuildEngine BuildEngine { get; set; } = null!;
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public string InputPath { get; set; } = "";
                public bool Execute()
                {
                    var abs = TaskEnvironment.GetAbsolutePath(InputPath);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("InputPath");
    }

    [Fact]
    public async Task NewAbsolutePath_WithLocalIndirection_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    var path = InputPath;
                    var abs = new AbsolutePath(path);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
    }

    [Fact]
    public async Task PathCombine_WithStringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string BasePath { get; set; } = "";
                public override bool Execute()
                {
                    var combined = Path.Combine(BasePath, "sub", "file.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("BasePath");
    }

    [Fact]
    public async Task HelperMethodWrapping_StringProp_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(FixPath(InputPath));
                    return true;
                }
                private static string FixPath(string p) => p.Replace('/', '\\');
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("InputPath");
    }

    // ── Negative cases for MSBuildTask0006 ──

    [Fact]
    public async Task NewAbsolutePath_FromLiteral_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var abs = new AbsolutePath("/some/path");
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewAbsolutePath_FromNonTaskProperty_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    string localPath = ComputePath();
                    var abs = new AbsolutePath(localPath);
                    return true;
                }
                private string ComputePath() => "/computed";
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task OutputProperty_Excluded_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Output]
                public string OutputPath { get; set; } = "";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(OutputPath);
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrivateProperty_Excluded_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                private string InternalPath { get; set; } = "";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InternalPath);
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadOnlyProperty_Excluded_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string ComputedPath { get; } = "/fixed";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(ComputedPath);
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonTaskClass_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class NotATask
            {
                public string InputPath { get; set; } = "";
                public void DoWork()
                {
                    var abs = new AbsolutePath(InputPath);
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0007: Prefer ITaskItem<T> over manual ItemSpec parsing
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task IntParse_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    int value = int.Parse(Item.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("int");
        diags[0].GetMessage().ShouldContain("Item");
    }

    [Fact]
    public async Task BoolParse_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Flag { get; set; } = null!;
                public override bool Execute()
                {
                    bool value = bool.Parse(Flag.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("bool");
    }

    [Fact]
    public async Task ConvertToInt32_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Count { get; set; } = null!;
                public override bool Execute()
                {
                    int value = Convert.ToInt32(Count.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("int");
    }

    [Fact]
    public async Task NewAbsolutePath_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem PathItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = new AbsolutePath(PathItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task NewFileInfo_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem FileItem { get; set; } = null!;
                public override bool Execute()
                {
                    var fi = new FileInfo(FileItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task GetAbsolutePath_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Framework.IMultiThreadableTask
            {
                public IBuildEngine BuildEngine { get; set; } = null!;
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public ITaskItem PathItem { get; set; } = null!;
                public bool Execute()
                {
                    var abs = TaskEnvironment.GetAbsolutePath(PathItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task IntParse_FromItemSpec_WithLocalIndirection_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    var spec = Item.ItemSpec;
                    int value = int.Parse(spec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task IntParse_FromArrayItemSpec_InForeach_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem[] Items { get; set; } = null!;
                public override bool Execute()
                {
                    foreach (var item in Items)
                    {
                        int value = int.Parse(item.ItemSpec);
                    }
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("ITaskItem[]");
    }

    [Fact]
    public async Task IntParse_FromArrayElementItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem[] Items { get; set; } = null!;
                public override bool Execute()
                {
                    int value = int.Parse(Items[0].ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task PathCombine_WithItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem OutputDir { get; set; } = null!;
                public ITaskItem OutputFile { get; set; } = null!;
                public override bool Execute()
                {
                    var combined = Path.Combine(OutputDir.ItemSpec, OutputFile.ItemSpec);
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task HelperMethodWrapping_ItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = new();
                public ITaskItem FileItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = TaskEnvironment.GetAbsolutePath(FixPath(FileItem.ItemSpec));
                    return true;
                }
                private static string FixPath(string p) => p.Replace('/', '\\');
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags.Length.ShouldBe(1);
        diags[0].GetMessage().ShouldContain("FileItem");
    }

    [Fact]
    public async Task ArrayProperty_SuggestsArrayType()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = new();
                public ITaskItem[] Directories { get; set; } = null!;
                public override bool Execute()
                {
                    foreach (ITaskItem item in Directories)
                    {
                        AbsolutePath abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
                    }
                    return true;
                }
            }
            """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBeGreaterThanOrEqualTo(1);
        // Should suggest array type, not singular
        task7.ShouldContain(d => d.GetMessage().Contains("AbsolutePath[]"));
        task7.ShouldNotContain(d => d.GetMessage().Contains("'AbsolutePath'") && !d.GetMessage().Contains("[]"));
    }

    [Fact]
    public async Task NewDirectoryInfo_FromAbsolutePathLocal_SuggestsDirectoryInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = new();
                public ITaskItem SourceDir { get; set; } = null!;
                public override bool Execute()
                {
                    AbsolutePath abs = TaskEnvironment.GetAbsolutePath(SourceDir.ItemSpec);
                    var dir = new DirectoryInfo(abs);
                    return true;
                }
            }
            """);

        // Should get MSBuildTask0007 for GetAbsolutePath(item.ItemSpec) AND for new DirectoryInfo(abs)
        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBeGreaterThanOrEqualTo(1);
        // At least one diagnostic should suggest DirectoryInfo
        task7.ShouldContain(d => d.GetMessage().Contains("DirectoryInfo"));
    }

    [Fact]
    public async Task NewFileInfo_FromAbsolutePathLocal_SuggestsFileInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = new();
                public ITaskItem DestFile { get; set; } = null!;
                public override bool Execute()
                {
                    AbsolutePath abs = TaskEnvironment.GetAbsolutePath(DestFile.ItemSpec);
                    var fi = new FileInfo(abs);
                    return true;
                }
            }
            """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBeGreaterThanOrEqualTo(1);
        task7.ShouldContain(d => d.GetMessage().Contains("FileInfo"));
    }

    // ── Negative cases for MSBuildTask0007 ──

    [Fact]
    public async Task IntParse_FromNonItemSpec_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    int value = int.Parse(Item.GetMetadata("Count"));
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task IntParse_FromStringProp_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string CountStr { get; set; } = "0";
                public override bool Execute()
                {
                    int value = int.Parse(CountStr);
                    return true;
                }
            }
            """);

        // MSBuildTask0007 should not fire; 0006 wouldn't either since int is not a path type
        diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ShouldBeEmpty();
    }

    [Fact]
    public async Task OutputItemProperty_Excluded_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Output]
                public ITaskItem ResultItem { get; set; } = null!;
                public override bool Execute()
                {
                    int value = int.Parse(ResultItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonTaskClass_ItemSpec_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class NotATask
            {
                public ITaskItem Item { get; set; } = null!;
                public void DoWork()
                {
                    int value = int.Parse(Item.ItemSpec);
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonMultiThreadableTask_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public ITaskItem FileItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InputPath);
                    int value = int.Parse(FileItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultiThreadableAttribute_ButNotITask_NoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class NotATask
            {
                public string InputPath { get; set; } = "";
                public ITaskItem FileItem { get; set; } = null!;
                public void DoWork()
                {
                    var abs = new AbsolutePath(InputPath);
                    int value = int.Parse(FileItem.ItemSpec);
                }
            }
            """);

        diags.ShouldBeEmpty();
    }
}
