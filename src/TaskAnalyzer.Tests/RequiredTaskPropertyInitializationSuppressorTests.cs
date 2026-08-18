// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class RequiredTaskPropertyInitializationSuppressorTests
{
    [Fact]
    public async Task RequiredProperty_InTask_DiagnosticIsSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Required]
                public string IldasmPath { get; set; }

                public override bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && d.IsSuppressed);
        diagnostics.ShouldNotContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }

    [Fact]
    public async Task RequiredProperty_InIndirectTaskSubclass_DiagnosticIsSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public abstract class BaseTask : Microsoft.Build.Utilities.Task
            {
            }

            public class MyTask : BaseTask
            {
                [Required]
                public string IldasmPath { get; set; }

                public override bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && d.IsSuppressed);
        diagnostics.ShouldNotContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }

    [Fact]
    public async Task MixedRequiredAndOptionalProperties_WithExplicitConstructor_SuppressesOnlyRequiredProperty()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public MyTask()
                {
                }

                [Required]
                public string RequiredValue { get; set; }

                public string OptionalValue { get; set; }

                public override bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        var cs8618Diagnostics = diagnostics.Where(d => d.Id == "CS8618").ToArray();
        cs8618Diagnostics.Length.ShouldBe(2);
        cs8618Diagnostics.Count(d => d.IsSuppressed).ShouldBe(1);
        cs8618Diagnostics.Count(d => !d.IsSuppressed).ShouldBe(1);
    }

    [Fact]
    public async Task DataAnnotationsRequired_InTask_DiagnosticIsNotSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using System.ComponentModel.DataAnnotations;

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Required]
                public string IldasmPath { get; set; }

                public override bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }

    [Fact]
    public async Task RequiredProperty_InDirectITaskImplementation_DiagnosticIsSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public class MyTask : ITask
            {
                public IBuildEngine BuildEngine { get; set; } = new BuildEngineStub();

                [Required]
                public string IldasmPath { get; set; }

                public bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && d.IsSuppressed);
        diagnostics.ShouldNotContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }

    [Fact]
    public async Task RequiredProperty_InNonTaskClass_DiagnosticIsNotSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public class MyTask
            {
                [Required]
                public string IldasmPath { get; set; }
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }

    [Fact]
    public async Task RequiredGetOnlyProperty_InTask_DiagnosticIsNotSuppressed()
    {
        var diagnostics = await GetCompilerAndAnalyzerDiagnosticsAsync(
            """
            using Microsoft.Build.Framework;

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                [Required]
                public string IldasmPath { get; }

                public override bool Execute() => true;
            }
            """,
            new RequiredTaskPropertyInitializationSuppressor());

        diagnostics.ShouldContain(d => d.Id == "CS8618" && !d.IsSuppressed);
    }
}
