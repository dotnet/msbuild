// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Shouldly;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="UnsupportedTaskItemTypeAnalyzer"/> covering MSBuildTask0009.
/// </summary>
[TestClass]
public class UnsupportedTaskItemTypeAnalyzerTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Value types are not yet supported by the task parameter binder
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("int")]
    [DataRow("bool")]
    [DataRow("long")]
    [DataRow("double")]
    [DataRow("float")]
    [DataRow("decimal")]
    [DataRow("byte")]
    [DataRow("sbyte")]
    [DataRow("short")]
    [DataRow("ushort")]
    [DataRow("uint")]
    [DataRow("ulong")]
    [DataRow("char")]
    [DataRow("System.DateTime")]
    [DataRow("string")]
    public async Task ValueType_ProducesDiagnostic(string typeName)
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync($$"""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<{{typeName}}> Item { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [TestMethod]
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

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [TestMethod]
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

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [TestMethod]
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

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Array variants
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SupportedPathTypeArray_NoDiagnostic()
    {
        var diags = await GetUnsupportedTaskItemTypeDiagnosticsAsync("""
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public ITaskItem<AbsolutePath>[] Items { get; set; } = null!;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.UnsupportedTaskItemType);
    }

    [TestMethod]
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

    [TestMethod]
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
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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
