// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using Shouldly;

using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public sealed class LogInputs_Tests
{
    private readonly ITestOutputHelper _output;

    public LogInputs_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Automatic task input logging covers only the parameters set in the project,
    /// so RAR keeps its own dump, which also lists defaults, when that logging is enabled.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogsInputsIncludingDefaults(bool taskInputLoggingEnabled)
    {
        MockEngine engine = RunResolveAssemblyReference(taskInputLoggingEnabled);

        engine.AssertLogContains("AllowedAssemblyExtensions");
        engine.AssertLogContains("IgnoreInstalledAssemblyTable");
    }

    [Fact]
    public void ChangeWaveOptOutSkipsInputsWhenTaskInputLoggingIsEnabled()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_14.ToString());
        ChangeWaves.ResetStateForTests();

        MockEngine engine = RunResolveAssemblyReference(taskInputLoggingEnabled: true);

        engine.AssertLogDoesntContain("AllowedAssemblyExtensions");
    }

    private MockEngine RunResolveAssemblyReference(bool taskInputLoggingEnabled)
    {
        MockEngine engine = new(_output)
        {
            SetIsTaskInputLoggingEnabled = taskInputLoggingEnabled,
        };

        ResolveAssemblyReference rar = new()
        {
            BuildEngine = engine,
            Assemblies = [new TaskItem("System.Runtime")],
            SearchPaths = ["{RawFileName}"],
        };

        rar.Execute().ShouldBeTrue();

        return engine;
    }
}
