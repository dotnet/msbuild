// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class AnalyzerConfigurationTests
{
    private const string UnsafeMtTask = """
        using System;
        using Microsoft.Build.Framework;

        public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; }

            public override bool Execute()
            {
                _ = Environment.CurrentDirectory;
                return true;
            }
        }
        """;

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("true", true)]
    [InlineData("", true)]
    [InlineData("invalid", true)]
    public async Task EnabledMsBuildPropertyUsesSafeDefault(string value, bool expectDiagnostic)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
            $"build_property.{SharedAnalyzerHelpers.EnabledPropertyKey}",
            value);

        diagnostics.Any(diagnostic => diagnostic.Id == DiagnosticIds.TaskEnvironmentRequired)
            .ShouldBe(expectDiagnostic);
    }

    [Fact]
    public async Task DirectGlobalConfigCanDisableAnalyzer()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
            SharedAnalyzerHelpers.EnabledOptionKey,
            "false");

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("all", true)]
    [InlineData("multithreadable_only", false)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    public async Task ScopeMsBuildPropertyUsesSafeDefault(string value, bool expectDiagnostic)
    {
        const string regularTask = """
            using System;

            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    _ = Environment.CurrentDirectory;
                    return true;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
            $"build_property.{SharedAnalyzerHelpers.ScopePropertyKey}",
            value,
            regularTask);

        diagnostics.Any(diagnostic => diagnostic.Id == DiagnosticIds.TaskEnvironmentRequired)
            .ShouldBe(expectDiagnostic);
    }

    private static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string key, string value) =>
        GetDiagnosticsAsync(key, value, UnsafeMtTask);

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string key, string value, string source)
    {
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { [key] = value }));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MultiThreadableTaskAnalyzer(),
            new TransitiveCallChainAnalyzer(),
            new PreferTypedParameterAnalyzer(),
            new UnsupportedTaskItemTypeAnalyzer(),
            new TaskEnvironmentConstructorInjectionAnalyzer());

        return await CreateCompilation(source)
            .WithAnalyzers(analyzers, analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }
}
