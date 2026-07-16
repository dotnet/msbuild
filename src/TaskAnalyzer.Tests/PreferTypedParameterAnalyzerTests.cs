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
    public async Task RelativeDefault_EmitsMoveToExecute_AndSuppressesPathDiagnostic()
    {
        // A relative default can't be rooted in the initializer, so the property is redirected from
        // MSBuildTask0006 to MSBuildTask0008 (initialize in Execute()); only the 0008 diagnostic is reported.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "obj";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InputPath);
                    return true;
                }
            }
            """);

        diags.Length.ShouldBe(1);
        diags[0].Id.ShouldBe(DiagnosticIds.InitializeRelativeDefaultInExecute);
        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
        diags[0].GetMessage().ShouldContain("InputPath");
        diags[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task FullyQualifiedDefault_StaysOnPathDiagnostic()
    {
        // A fully-qualified default can be reproduced in the initializer, so it stays on MSBuildTask0006.
        var diags = await GetTypedParameterDiagnosticsAsync($$"""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "{{TestHelpers.FullyQualifiedPath("work/obj")}}";
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InputPath);
                    return true;
                }
            }
            """);

        diags.Length.ShouldBe(1);
        diags[0].Id.ShouldBe(DiagnosticIds.PreferTypedPathParameter);
        diags.ShouldNotContain(d => d.Id == DiagnosticIds.InitializeRelativeDefaultInExecute);
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
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
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
    public async Task TryParse_FromItemSpec_ProducesNoDiagnostic()
    {
        // TryParse is defensive (bool result + out parameter). Suggesting ITaskItem<int> would change
        // error handling to a bind-time throw, and the code fix can never rewrite its multi-argument shape,
        // so the analyzer must not flag it.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem Count { get; set; } = null!;
                public override bool Execute()
                {
                    return int.TryParse(Count.ItemSpec, out _);
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
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
    public async Task NewAbsolutePath_FromGetMetadataFullPath_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem PathItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = new AbsolutePath(PathItem.GetMetadata("FullPath"));
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task NewFileInfo_FromGetMetadataFullPath_CaseInsensitive_ProducesDiagnostic()
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
                    var fi = new FileInfo(FileItem.GetMetadata("fullpath"));
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task NewAbsolutePath_FromGetMetadataOtherName_DoesNotProduceItemDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem PathItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = new AbsolutePath(PathItem.GetMetadata("Culture"));
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task GetAbsolutePath_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
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
    public async Task InheritsMultiThreadableInterface_WithoutAttribute_ProducesNoDiagnostic()
    {
        // The task derives from a base that implements IMultiThreadableTask but does not itself carry the
        // [MSBuildMultiThreadableTask] attribute (which is Inherited = false), so it has not opted into
        // multithreaded support and must not be analyzed.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public abstract class BaseTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
            }
            public class MyTask : BaseTask
            {
                public ITaskItem PathItem { get; set; } = null!;
                public override bool Execute()
                {
                    var abs = new AbsolutePath(PathItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task InputPropertyDeclaredOnBaseClass_WithAttribute_ProducesDiagnostic()
    {
        // The input property is declared on a base class while the attribute is on the derived task. Applicability
        // must consider inherited properties, not just those declared directly on the attributed type.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public abstract class BaseTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem PathItem { get; set; } = null!;
            }
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : BaseTask
            {
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

        // Only the first argument is flagged: Path.Combine restarts from the last rooted segment, so making
        // a later argument absolute could change the result. OutputDir (first) is flagged; OutputFile is not.
        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("OutputDir");
    }

    [Fact]
    public async Task PathCombine_StringProp_NotFirstArgument_ProducesNoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string SubPath { get; set; } = "";
                public override bool Execute()
                {
                    var combined = Path.Combine("base", SubPath);
                    return true;
                }
            }
            """);

        // SubPath only appears in a non-first position, where suggesting AbsolutePath could change semantics.
        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedPathParameter);
    }

    [Fact]
    public async Task PathCombine_ItemSpec_NotFirstArgument_ProducesNoDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem SubItem { get; set; } = null!;
                public override bool Execute()
                {
                    var combined = Path.Combine("base", SubItem.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
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
        // Should suggest ITaskItem<AbsolutePath>[] — brackets outside the angle brackets
        task7.ShouldContain(d => d.GetMessage().Contains("ITaskItem<AbsolutePath>[]"));
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

        // The AbsolutePath suggestion should be suppressed in favor of DirectoryInfo
        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("DirectoryInfo");
        task7[0].GetMessage().ShouldNotContain("AbsolutePath");
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
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("FileInfo");
        task7[0].GetMessage().ShouldNotContain("AbsolutePath");
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

    // ── System.IO consumption-site inference (File.* => FileInfo, Directory.* => DirectoryInfo) ──

    [Fact]
    public async Task FileDelete_OnItemSpec_SuggestsFileInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem Target { get; set; } = null!;
            public override bool Execute()
            {
                File.Delete(Target.ItemSpec);
                return true;
            }
        }
        """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task DirectoryCreate_OnItemSpec_SuggestsDirectoryInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem DestinationFolder { get; set; } = null!;
            public override bool Execute()
            {
                Directory.CreateDirectory(DestinationFolder.ItemSpec);
                return true;
            }
        }
        """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("DirectoryInfo");
    }

    [Fact]
    public async Task FileReadAllLines_ThroughAbsolutePath_SuggestsFileInfoAndSuppressesAbsolutePath()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem Input { get; set; } = null!;
            public override bool Execute()
            {
                var lines = File.ReadAllLines(TaskEnvironment.GetAbsolutePath(Input.ItemSpec));
                return true;
            }
        }
        """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("FileInfo");
        task7[0].GetMessage().ShouldNotContain("AbsolutePath");
    }

    [Fact]
    public async Task NewFileStream_OnItemSpec_SuggestsFileInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem XmlInputPath { get; set; } = null!;
            public override bool Execute()
            {
                using var stream = new FileStream(XmlInputPath.ItemSpec, FileMode.Open);
                return true;
            }
        }
        """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task FileWriteAllText_DoesNotFlagNonPathContentsArgument()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem OutputFile { get; set; } = null!;
            public ITaskItem Contents { get; set; } = null!;
            public override bool Execute()
            {
                File.WriteAllText(OutputFile.ItemSpec, Contents.ItemSpec);
                return true;
            }
        }
        """);

        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("OutputFile");
        task7[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task DirectoryCreate_OnReassignedNullableLocal_SuggestsDirectoryInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; } = new();
                public ITaskItem Directories { get; set; } = null!;
                public override bool Execute()
                {
                    AbsolutePath? absolutePath = null;
                    absolutePath = TaskEnvironment.GetAbsolutePath(Directories.ItemSpec);
                    Directory.CreateDirectory(absolutePath);
                    return true;
                }
            }
            """);

        // The local is declared `= null` then assigned once; the consumption site still resolves to DirectoryInfo.
        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("DirectoryInfo");
        task7[0].GetMessage().ShouldNotContain("AbsolutePath");
    }

    [Fact]
    public async Task FileAndDirectoryConflict_FallsBackToAbsolutePath()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public ITaskItem Ambiguous { get; set; } = null!;
            public override bool Execute()
            {
                AbsolutePath abs = TaskEnvironment.GetAbsolutePath(Ambiguous.ItemSpec);
                File.Delete(abs);
                Directory.CreateDirectory(abs);
                return true;
            }
        }
        """);

        // Contradictory file/dir inference for one property: keep the AbsolutePath fallback, drop the specifics.
        var task7 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedTaskItem).ToArray();
        task7.Length.ShouldBe(1);
        task7[0].GetMessage().ShouldContain("AbsolutePath");
        task7[0].GetMessage().ShouldNotContain("FileInfo");
        task7[0].GetMessage().ShouldNotContain("DirectoryInfo");
    }

    [Fact]
    public async Task FileAndDirectoryConflict_NoAbsolutePathSite_StillFlagsWithAbsolutePath()
    {
        // A string property consumed as BOTH a file and a directory, with no AbsolutePath site to carry the
        // suggestion. Neither FileInfo nor DirectoryInfo is safe, so the property must still be flagged, but
        // with the AbsolutePath fallback rather than emitting two contradictory specific suggestions.
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string Ambiguous { get; set; } = "";
            public override bool Execute()
            {
                File.Delete(Ambiguous);
                Directory.CreateDirectory(Ambiguous);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBeGreaterThan(0);
        foreach (var diag in task6)
        {
            diag.GetMessage().ShouldContain("Ambiguous");
            diag.GetMessage().ShouldContain("AbsolutePath");
            diag.GetMessage().ShouldNotContain("FileInfo");
            diag.GetMessage().ShouldNotContain("DirectoryInfo");
        }
    }

    // ── MSBuildTask0006 over raw-string 0002/0003 scenarios (one-shot instead of daisy-chaining) ──

    [Fact]
    public async Task PathGetFullPath_OnStringProp_SuggestsAbsolutePath()
    {
        // Path.GetFullPath(prop) is the raw normalization that MSBuildTask0002 flags. MSBuildTask0006 also
        // surfaces it so the property can be retyped in one shot.
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string InputPath { get; set; } = "";
            public override bool Execute()
            {
                var full = Path.GetFullPath(InputPath);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("InputPath");
        task6[0].GetMessage().ShouldContain("AbsolutePath");
    }

    [Fact]
    public async Task FileDelete_OnStringProp_SuggestsFileInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string Target { get; set; } = "";
            public override bool Execute()
            {
                File.Delete(Target);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("Target");
        task6[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task DirectoryCreate_OnStringProp_SuggestsDirectoryInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string DestinationFolder { get; set; } = "";
            public override bool Execute()
            {
                Directory.CreateDirectory(DestinationFolder);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("DestinationFolder");
        task6[0].GetMessage().ShouldContain("DirectoryInfo");
    }

    [Fact]
    public async Task NewFileStream_OnStringProp_SuggestsFileInfo()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string InputPath { get; set; } = "";
            public override bool Execute()
            {
                using var stream = new FileStream(InputPath, FileMode.Open);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("InputPath");
        task6[0].GetMessage().ShouldContain("FileInfo");
    }

    [Fact]
    public async Task FileDelete_OnAlreadyWrappedStringProp_SuggestsAbsolutePathNotFileInfo()
    {
        // The argument is already rooted through TaskEnvironment.GetAbsolutePath, so the raw-consumption rule
        // must not fire a FileInfo suggestion. The inner GetAbsolutePath(prop) still yields the standard
        // AbsolutePath suggestion.
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string Target { get; set; } = "";
            public override bool Execute()
            {
                File.Delete(TaskEnvironment.GetAbsolutePath(Target));
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("AbsolutePath");
        task6[0].GetMessage().ShouldNotContain("FileInfo");
    }

    [Fact]
    public async Task FileWriteAllText_DoesNotFlagNonPathContentsArgument_StringProp()
    {
        // Only the path-named parameter is flagged; the "contents" string argument is not a path.
        var diags = await GetTypedParameterDiagnosticsAsync("""
        using System.IO;
        using Microsoft.Build.Framework;
        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new();
            public string OutputFile { get; set; } = "";
            public string Contents { get; set; } = "";
            public override bool Execute()
            {
                File.WriteAllText(OutputFile, Contents);
                return true;
            }
        }
        """);

        var task6 = diags.Where(d => d.Id == DiagnosticIds.PreferTypedPathParameter).ToArray();
        task6.Length.ShouldBe(1);
        task6[0].GetMessage().ShouldContain("OutputFile");
        task6[0].GetMessage().ShouldContain("FileInfo");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Fix #5: Restrict to ValueTypeParser-supported types only
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GuidParse_FromItemSpec_NoDiagnostic()
    {
        // Guid is not supported by ValueTypeParser, so no diagnostic should be emitted.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    var id = Guid.Parse(Item.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task TimeSpanParse_FromItemSpec_NoDiagnostic()
    {
        // TimeSpan is not supported by ValueTypeParser, so no diagnostic should be emitted.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    var ts = TimeSpan.Parse(Item.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task IntParse_FromItemSpec_StillProducesDiagnostic()
    {
        // int is supported by ValueTypeParser, so the diagnostic should still fire.
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public ITaskItem Item { get; set; } = null!;
                public override bool Execute()
                {
                    var n = int.Parse(Item.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
    }

    [Fact]
    public async Task DateTimeParse_FromItemSpec_ProducesDiagnostic()
    {
        var diags = await GetTypedParameterDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IMultiThreadableTask
            {
                public ITaskItem Item { get; set; } = null!;
                public TaskEnvironment TaskEnvironment { get; set; } = null!;
                public override bool Execute()
                {
                    var timestamp = DateTime.Parse(Item.ItemSpec);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PreferTypedTaskItem);
        diags[0].GetMessage().ShouldContain("DateTime");
    }
}
