// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="UnsupportedTaskItemTypeAnalyzer"/> covering MSBuildTask0009 and MSBuildTask0010.
/// </summary>
public class UnsupportedTaskItemTypeAnalyzerTests
{
    [Theory]
    [InlineData("bool")]
    [InlineData("string")]
    public async Task DedicatedParserType_NoDiagnostic(string typeName)
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync($$"""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<{{typeName}}> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("char")]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("decimal")]
    [InlineData("System.DateTime")]
    public async Task ConvertChangeTypeType_ProducesError(string typeName)
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync($$"""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<{{typeName}}> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
        Diagnostic diagnostic = diags.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DiagnosticIds.CultureSensitiveTaskItemType);
        diagnostic.Severity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Convert.ChangeType");
        diagnostic.GetMessage().ShouldContain("CultureInfo.InvariantCulture");
    }

    [Fact]
    public async Task AbsolutePath_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<AbsolutePath> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileInfo_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<FileInfo> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectoryInfo_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<DirectoryInfo> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Array variants
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ConvertChangeTypeArray_ProducesError()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<int>[] Items { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diags.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DiagnosticIds.CultureSensitiveTaskItemType);
        diagnostic.Severity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SupportedOutputProperty_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Output]
                public ITaskItem<string> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConvertChangeTypeOutputProperty_ProducesError()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Output]
                public ITaskItem<decimal> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        Diagnostic diagnostic = diags.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DiagnosticIds.CultureSensitiveTaskItemType);
        diagnostic.Severity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task OutputProperty_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Output]
                public ITaskItem<Guid> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Unsupported types — diagnostic expected
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Guid_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<Guid> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
        diags[0].GetMessage().ShouldContain("Item");
        diags[0].GetMessage().ShouldContain("Guid");
        diags[0].GetMessage().ShouldContain("string, bool, AbsolutePath, FileInfo, DirectoryInfo");
        diags[0].GetMessage().ShouldNotContain("int,");
    }

    [Fact]
    public async Task TimeSpan_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<TimeSpan> Duration { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
        diags[0].GetMessage().ShouldContain("Duration");
        diags[0].GetMessage().ShouldContain("TimeSpan");
    }

    [Fact]
    public async Task Enum_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public enum Mode { First, Second }
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<Mode> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [Fact]
    public async Task NullableValueType_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<int?> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [Fact]
    public async Task CustomStruct_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public struct CustomValue { }
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<CustomValue> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [Fact]
    public async Task UnsupportedTypeArray_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<Guid>[] Items { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [Fact]
    public async Task InheritedProperty_ProducesDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class BaseParameters
            {
                public ITaskItem<Guid> Item { get; set; } = null!;
            }
            public class MyTask : BaseParameters, ITask
            {
                public IBuildEngine BuildEngine { get; set; } = new BuildEngineStub();
                public bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [Fact]
    public async Task StaticProperty_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public static ITaskItem<Guid> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Non-task classes — no diagnostic expected
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NonTaskClass_NoDiagnostic()
    {
        // A class that does not implement ITask should not trigger the diagnostic,
        // even if it has an ITaskItem<Guid> property.
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class NotATask
            {
                public ITaskItem<Guid> Item { get; set; } = null!;
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }
}
