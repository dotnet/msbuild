// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests;

/// <summary>
/// End to end tests for the terminal logger configuration.
/// We need to execute msbuild process as tested code path is also in XMake.cs.
/// Also verifies that the telemetry is logged correctly.
/// Because we need to test the telemetry for the terminal logger, we need to use the MockLogger which outputs all telemetry properties.
/// </summary>
public class TerminalLoggerConfiguration_Tests : IDisposable
{
    private readonly TestEnvironment _env;

    private readonly string _cmd;

    public TerminalLoggerConfiguration_Tests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);

        // Ignore environment variables that may have been set by the environment where the tests are running.
        _env.SetEnvironmentVariable("MSBUILDLIVELOGGER", null);
        _env.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", null);

        TransientTestFolder logFolder = _env.CreateFolder(createFolder: true);
        TransientTestFile projectFile = _env.CreateFile(logFolder, "myProj.proj", $"""
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" DefaultTargets="Hello">
                <Target Name="Hello">
                  <Message Text="Hello, world!" />
                </Target>
            </Project>
            """);
        _cmd = $"{projectFile.Path} -target:Hello -logger:{typeof(MockLogger).FullName},{typeof(MockLogger).Assembly.Location};ReportTelemetry";
    }

    /// <summary>
    /// TearDown
    /// </summary>
    public void Dispose()
    {
        _env.Dispose();
    }

    [Theory]
    [InlineData("on")]
    [InlineData("true")]
    public void TerminalLoggerOn(string tlValue)
    {
        string output = RunnerUtilities.ExecMSBuild($"{_cmd} -tl:{tlValue}", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = true,
            TerminalLoggerDefault = bool.FalseString,
            TerminalLoggerDefaultSource = "msbuild",
            TerminalLoggerUserIntent = tlValue,
            TerminalLoggerUserIntentSource = "arg",
            ConsoleLogger = false,
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldBeTerminalLog(output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("auto")]
    public void TerminalLoggerWithTlAutoIsOff(string tlValue)
    {
        string output = RunnerUtilities.ExecMSBuild($"{_cmd} -tl:{tlValue}", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = false,
            TerminalLoggerDefault = bool.FalseString,
            TerminalLoggerDefaultSource = "msbuild",
            TerminalLoggerUserIntent = tlValue,
            TerminalLoggerUserIntentSource = "arg",
            ConsoleLogger = true,
            ConsoleLoggerType = "parallel",
            ConsoleLoggerVerbosity = "normal",
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldNotBeTerminalLog(output);
    }

    [Fact]
    public void TerminalLoggerDefaultByEnv()
    {
        _env.SetEnvironmentVariable("DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER", bool.TrueString);
        string output = RunnerUtilities.ExecMSBuild($"{_cmd} -tlp:default={bool.TrueString}", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = true,
            TerminalLoggerDefault = bool.TrueString,
            TerminalLoggerDefaultSource = "DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER",
            TerminalLoggerUserIntent = null,
            TerminalLoggerUserIntentSource = null,
            ConsoleLogger = false,
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldBeTerminalLog(output);
    }

    [Theory]
    [InlineData("MSBUILDLIVELOGGER")]
    [InlineData("MSBUILDTERMINALLOGGER")]
    public void TerminalLoggerOnByEnv(string envVarSource)
    {
        _env.SetEnvironmentVariable(envVarSource, bool.TrueString);
        string output = RunnerUtilities.ExecMSBuild($"{_cmd}", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = true,
            TerminalLoggerDefault = bool.FalseString,
            TerminalLoggerDefaultSource = "msbuild",
            TerminalLoggerUserIntent = bool.TrueString,
            TerminalLoggerUserIntentSource = envVarSource,
            ConsoleLogger = false,
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldBeTerminalLog(output);
    }

    [Theory]
    [InlineData("on")]
    [InlineData("true")]
    public void TerminalLoggerDefaultOn(string defaultValue)
    {
        string output = RunnerUtilities.ExecMSBuild($"{_cmd} -tlp:default={defaultValue}", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = true,
            TerminalLoggerDefault = defaultValue,
            TerminalLoggerDefaultSource = "sdk",
            TerminalLoggerUserIntent = null,
            TerminalLoggerUserIntentSource = null,
            ConsoleLogger = false,
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldBeTerminalLog(output);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("false")]
    public void TerminalLoggerDefaultOff(string defaultValue)
    {
        string output = RunnerUtilities.ExecMSBuild($"{_cmd} -tlp:default={defaultValue} -v:m", out bool success);
        success.ShouldBeTrue();

        LoggingConfigurationTelemetry expectedTelemetry = new LoggingConfigurationTelemetry
        {
            TerminalLogger = false,
            TerminalLoggerDefault = defaultValue,
            TerminalLoggerDefaultSource = "sdk",
            TerminalLoggerUserIntent = null,
            TerminalLoggerUserIntentSource = null,
            ConsoleLogger = true,
            ConsoleLoggerVerbosity = "minimal",
            ConsoleLoggerType = "parallel",
            FileLogger = false,
        };

        expectedTelemetry.UpdateEventProperties();
        foreach (KeyValuePair<string, string> pair in expectedTelemetry.Properties)
        {
            output.ShouldContain($"{expectedTelemetry.EventName}:{pair.Key}={pair.Value}");
        }

        // Test if there is ANSI clear screen sequence, which shall only occur when the terminal logger was enabled.
        ShouldNotBeTerminalLog(output);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    public void TerminalLoggerOnInvalidProjectBuild(string msbuildinprocnodeState)
    {
        _ = _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", msbuildinprocnodeState);

        string output = RunnerUtilities.ExecMSBuild(
            $"{_cmd} -tl:true",
            out bool success);

        success.ShouldBeTrue();
        ShouldBeTerminalLog(output);
        output.ShouldContain("Build succeeded.");
    }

    private static void ShouldBeTerminalLog(string output) => output.ShouldContain("\x1b[?25l");

    private static void ShouldNotBeTerminalLog(string output) => output.ShouldNotContain("\x1b[?25l");
}
