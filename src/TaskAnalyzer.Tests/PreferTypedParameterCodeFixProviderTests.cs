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
        DiagnosticIds.InitializeRelativeDefaultInExecute => new DiagnosticResult(DiagnosticDescriptors.InitializeRelativeDefaultInExecute),
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
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
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
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
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
    public async Task Fix_0007_NewAbsolutePath_FromGetMetadataFullPath_RetypesTaskItemAndUsesValue()
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
                        var path = {|#0:new AbsolutePath(Item.GetMetadata("FullPath"))|};
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
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

    [Fact]
    public async Task Fix_0006_PathGetFullPath_RetypesToAbsolutePathAndRemovesCall()
    {
        // Path.GetFullPath(prop) is the raw MSBuildTask0002 normalization. Retyping the property to AbsolutePath
        // makes the value already-absolute, so the whole call collapses to the property (one-shot, no daisy-chain).
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "";
                    public override bool Execute()
                    {
                        var full = {|#0:Path.GetFullPath(InputPath)|};
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
                    public AbsolutePath InputPath { get; set; } = default;
                    public override bool Execute()
                    {
                        var full = InputPath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath")).RunAsync();
    }

    [Fact]
    public async Task Fix_0006_FileConsumption_RetypesToFileInfoAndUsesFullName()
    {
        // File.Delete(prop) is the raw MSBuildTask0003 consumption. Retyping to FileInfo requires feeding the
        // string API through .FullName (FileInfo has no implicit string conversion).
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string Target { get; set; } = "";
                    public override bool Execute()
                    {
                        {|#0:File.Delete(Target)|};
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
                    public FileInfo Target { get; set; } = null!;
                    public override bool Execute()
                    {
                        File.Delete(Target.FullName);
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("Target", "string", "FileInfo")).RunAsync();
    }

    [Fact]
    public async Task Fix_0006_DirectoryConsumption_RetypesToDirectoryInfoAndUsesFullName()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string DestinationFolder { get; set; } = "";
                    public override bool Execute()
                    {
                        {|#0:Directory.CreateDirectory(DestinationFolder)|};
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
                    public DirectoryInfo DestinationFolder { get; set; } = null!;
                    public override bool Execute()
                    {
                        Directory.CreateDirectory(DestinationFolder.FullName);
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("DestinationFolder", "string", "DirectoryInfo")).RunAsync();
    }

    [Fact]
    public async Task Fix_0006_AbsolutePathDefault_NormalizedThroughAbsolutePath()
    {
        // A fully-qualified string default is preserved after retyping by constructing the AbsolutePath in the
        // initializer. This is only safe because the value is already rooted: MSBuild normally roots a relative
        // string via TaskEnvironment, which is not available inside a property initializer (it runs in the ctor).
        await CreateFixTest(
            testCode: $$"""
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "{{TestHelpers.FullyQualifiedPath("logs")}}";
                    public override bool Execute()
                    {
                        var abs = {|#0:new AbsolutePath(InputPath)|};
                        return true;
                    }
                }
                """,
            fixedCode: $$"""
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public AbsolutePath InputPath { get; set; } = new AbsolutePath("{{TestHelpers.FullyQualifiedPath("logs")}}");
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
    public async Task Fix_0006_AbsolutePathDefault_FileInfo_NormalizedThroughAbsolutePath()
    {
        // For FileInfo the engine goes string -> AbsolutePath -> FileInfo, so the fix constructs through
        // AbsolutePath rather than passing the raw string to FileInfo directly.
        await CreateFixTest(
            testCode: $$"""
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string FilePath { get; set; } = "{{TestHelpers.FullyQualifiedPath("logs/out.txt")}}";
                    public override bool Execute()
                    {
                        var fi = {|#0:new FileInfo(FilePath)|};
                        return true;
                    }
                }
                """,
            fixedCode: $$"""
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public FileInfo FilePath { get; set; } = new FileInfo(new AbsolutePath("{{TestHelpers.FullyQualifiedPath("logs/out.txt")}}"));
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
    public async Task Fix_0008_RelativeDefault_AbsolutePath_InitializedInExecute()
    {
        // A relative default cannot be rooted in a property initializer (TaskEnvironment is only available after
        // construction), so MSBuildTask0008 moves it into Execute() with a guarded assignment that only applies
        // when the property is still unset (so a value bound from the project XML is not clobbered).
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string InputPath { get; set; } = {|#0:"obj"|};
                    public override bool Execute()
                    {
                        var abs = new AbsolutePath(InputPath);
                        return true;
                    }
                }
                """,
            fixedCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public AbsolutePath InputPath { get; set; } = default;
                    public override bool Execute()
                    {
                        if (InputPath == default)
                        {
                            InputPath = TaskEnvironment.GetAbsolutePath("obj");
                        }

                        var abs = InputPath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.InitializeRelativeDefaultInExecute).WithLocation(0)
                .WithArguments("InputPath", "AbsolutePath")).RunAsync();
    }

    [Fact]
    public async Task Fix_0008_RelativeDefault_FileInfo_InitializedInExecute()
    {
        // For a reference type the guard is a null-coalescing assignment, and the value is built through the
        // string -> AbsolutePath -> FileInfo chain (AbsolutePath converts implicitly to string).
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string LogPath { get; set; } = {|#0:"logs"|};
                    public override bool Execute()
                    {
                        var fi = new FileInfo(LogPath);
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public FileInfo LogPath { get; set; } = null!;
                    public override bool Execute()
                    {
                        LogPath ??= new FileInfo(TaskEnvironment.GetAbsolutePath("logs"));
                        var fi = LogPath;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.InitializeRelativeDefaultInExecute).WithLocation(0)
                .WithArguments("LogPath", "FileInfo")).RunAsync();
    }

    [Fact]
    public async Task Fix_0008_RelativeDefault_DirectoryInfo_InitializedInExecute()
    {
        await CreateFixTest(
            testCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public string OutputDir { get; set; } = {|#0:"bin"|};
                    public override bool Execute()
                    {
                        var di = new DirectoryInfo(OutputDir);
                        return true;
                    }
                }
                """,
            fixedCode: """
                using System.IO;
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
                {
                    public TaskEnvironment TaskEnvironment { get; set; }
                    public DirectoryInfo OutputDir { get; set; } = null!;
                    public override bool Execute()
                    {
                        OutputDir ??= new DirectoryInfo(TaskEnvironment.GetAbsolutePath("bin"));
                        var di = OutputDir;
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.InitializeRelativeDefaultInExecute).WithLocation(0)
                .WithArguments("OutputDir", "DirectoryInfo")).RunAsync();
    }

    [Fact]
    public async Task Fix_0008_NoTaskEnvironment_NoFixOffered()
    {
        // Without an accessible TaskEnvironment member the relative default cannot be rooted, so the diagnostic
        // is reported but no fix is offered.
        await CreateNoFixTest(
            """
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = {|#0:"obj"|};
                public override bool Execute()
                {
                    var abs = new AbsolutePath(InputPath);
                    return true;
                }
            }
            """,
            Diag(DiagnosticIds.InitializeRelativeDefaultInExecute).WithLocation(0)
                .WithArguments("InputPath", "AbsolutePath"));
    }

    [Fact]
    public async Task Fix_0008_ExpressionBodiedExecute_NoFixOffered()
    {
        // An expression-bodied Execute() has no statement block to insert the guarded initialization into, so
        // the diagnostic is reported but no fix is offered.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string InputPath { get; set; } = {|#0:"obj"|};
                public override bool Execute() => File.Exists(new AbsolutePath(InputPath));
            }
            """,
            Diag(DiagnosticIds.InitializeRelativeDefaultInExecute).WithLocation(0)
                .WithArguments("InputPath", "AbsolutePath"));
    }

    [Fact]
    public async Task Fix_0006_NonConstantDefault_NoFixOffered()
    {
        // The default is not a compile-time constant, so it can't be safely re-expressed as the new type.
        // The diagnostic is still reported, but no code fix is offered (the source is left unchanged).
        await CreateNoFixTest(
            """
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = System.IO.Path.GetTempPath();
                public override bool Execute()
                {
                    var abs = {|#0:new AbsolutePath(InputPath)|};
                    return true;
                }
            }
            """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath"));
    }

    [Fact]
    public async Task Fix_0006_InvalidPathDefault_NoFixOffered()
    {
        // The default contains a character that is never valid in a path, so it isn't a plausible path value.
        // The diagnostic is still reported, but no code fix is offered.
        await CreateNoFixTest(
            """
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "bad\u0000path";
                public override bool Execute()
                {
                    var abs = {|#0:new AbsolutePath(InputPath)|};
                    return true;
                }
            }
            """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath"));
    }

    [Fact]
    public async Task Fix_0006_PartialClass_SingleFile_AppliesFix()
    {
        // The property is declared in one partial part and converted in another (same file); the fix must
        // find the property via its symbol and rewrite the conversion.
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public partial class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "";
                }
                public partial class MyTask
                {
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
                public partial class MyTask : Microsoft.Build.Utilities.Task
                {
                    public AbsolutePath InputPath { get; set; } = default;
                }
                public partial class MyTask
                {
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
    public async Task Fix_0006_PartialClass_NonRewritableReferenceInOtherFile_NoFixOffered()
    {
        // A partial part in another file uses the property as a plain string. Retyping it there would not
        // compile, and a single-document fix can't rewrite the other file, so no fix must be offered.
        const string file1 = """
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public partial class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    var abs = {|#0:new AbsolutePath(InputPath)|};
                    return true;
                }
            }
            """;
        const string file2 = """
            public partial class MyTask
            {
                private string Echo() => InputPath;
            }
            """;

        var test = new CSharpCodeFixTest<PreferTypedParameterAnalyzer, PreferTypedParameterCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("File1.cs", file1));
        test.TestState.Sources.Add(("File2.cs", file2));
        test.TestState.Sources.Add(("Stubs.cs", FrameworkStubs));
        test.FixedState.Sources.Add(("File1.cs", file1));
        test.FixedState.Sources.Add(("File2.cs", file2));
        test.FixedState.Sources.Add(("Stubs.cs", FrameworkStubs));

        var diag = Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
            .WithArguments("InputPath", "string", "AbsolutePath");
        test.TestState.ExpectedDiagnostics.Add(diag);
        test.FixedState.ExpectedDiagnostics.Add(diag);

        await test.RunAsync();
    }

    /// <summary>
    /// Builds a code-fix test where the diagnostic is expected but no fix is offered: the fixed source is
    /// identical to the test source, so applying any offered fix would fail the comparison.
    /// </summary>
    [Fact]
    public async Task Fix_0006_FileInfo_NullGuardedReference_NoFixOffered()
    {
        // The property is consumed by string.IsNullOrEmpty(prop), which is deliberately null-tolerant. Rewriting
        // that argument to prop.FullName after retyping to FileInfo would dereference a possibly-null property and
        // throw at runtime, so the diagnostic is reported but no fix is offered.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string InputPath { get; set; } = "";
                public override bool Execute()
                {
                    if (!string.IsNullOrEmpty(InputPath))
                    {
                        var fi = {|#0:new FileInfo(InputPath)|};
                    }
                    return true;
                }
            }
            """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "FileInfo"));
    }

    [Fact]
    public async Task Fix_0006_DirectoryInfo_NullWhiteSpaceGuardedReference_NoFixOffered()
    {
        // Same null-guard hazard via string.IsNullOrWhiteSpace(prop) for a DirectoryInfo retype.
        await CreateNoFixTest(
            """
            using System.IO;
            using Microsoft.Build.Framework;
            [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public string OutputDir { get; set; } = "";
                public override bool Execute()
                {
                    if (!string.IsNullOrWhiteSpace(OutputDir))
                    {
                        var di = {|#0:new DirectoryInfo(OutputDir)|};
                    }
                    return true;
                }
            }
            """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("OutputDir", "string", "DirectoryInfo"));
    }

    [Fact]
    public async Task Fix_0006_AbsolutePath_NullGuardedReference_StillFixed()
    {
        // AbsolutePath is a struct with an implicit string conversion, so string.IsNullOrEmpty(prop) compiles
        // unchanged after the retype and cannot NRE — the fix is still safely offered.
        await CreateFixTest(
            testCode: """
                using Microsoft.Build.Framework;
                [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
                public class MyTask : Microsoft.Build.Utilities.Task
                {
                    public string InputPath { get; set; } = "";
                    public override bool Execute()
                    {
                        if (!string.IsNullOrEmpty(InputPath))
                        {
                            var abs = {|#0:new AbsolutePath(InputPath)|};
                        }
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
                        if (!string.IsNullOrEmpty(InputPath))
                        {
                            var abs = InputPath;
                        }
                        return true;
                    }
                }
                """,
            Diag(DiagnosticIds.PreferTypedPathParameter).WithLocation(0)
                .WithArguments("InputPath", "string", "AbsolutePath")).RunAsync();
    }

    private static async Task CreateNoFixTest(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<PreferTypedParameterAnalyzer, PreferTypedParameterCodeFixProvider, DefaultVerifier>
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
