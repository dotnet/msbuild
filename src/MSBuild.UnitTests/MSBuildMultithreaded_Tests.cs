// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{

    /// <summary>
    /// Test task that implements IMultiThreadableTask and verifies environment isolation.
    /// This task checks that TaskEnvironment is properly provided and tests different
    /// environment variable behavior between multithreaded and single-threaded modes.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class EnvironmentIsolationTestTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        /// <summary>
        /// Indicates whether this task is expected to run in multithreaded mode.
        /// Used to verify different environment variable behavior.
        /// </summary>
        public bool IsMultithreadedMode { get; set; } = false;

        public override bool Execute()
        {
            if (!VerifyTaskEnvironment())
            {
                return false;
            }

            // Test environment variable behavior based on mode
            return TestEnvironmentIsolation();
        }

        private bool VerifyTaskEnvironment()
        {
            if (TaskEnvironment == null)
            {
                Log.LogError("TaskEnvironment was not provided to multithreadable task");
                return false;
            }

            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory.Value))
            {
                Log.LogError("TaskEnvironment.ProjectDirectory is null or empty");
                return false;
            }

            return true;
        }

        private bool TestEnvironmentIsolation()
        {
            string mode = IsMultithreadedMode ? "MultiThreaded" : "MultiProcess";
            string envVarName = $"MSBUILD_MULTITHREADED_TEST_VAR_{Guid.NewGuid():N}";
            string envVarValue = "TestValue";

            // Set environment variable using TaskEnvironment
            TaskEnvironment.SetEnvironmentVariable(envVarName, envVarValue);

            // Read using both TaskEnvironment and Environment.GetEnvironmentVariable
            string? taskEnvValue = TaskEnvironment.GetEnvironmentVariable(envVarName);
            string? globalEnvValue = Environment.GetEnvironmentVariable(envVarName);

            // Verify TaskEnvironment always works correctly
            if (taskEnvValue != envVarValue)
            {
                Log.LogError($"{mode} Mode: TaskEnvironment failed to read back value. Set: {envVarValue}, Read: {taskEnvValue}");
                return false;
            }

            if (IsMultithreadedMode)
            {
                // TaskEnvironment and Environment.GetEnvironmentVariable should differ
                if (taskEnvValue == globalEnvValue)
                {
                    Log.LogError($"{mode} Mode: Expected TaskEnvironment to be isolated, but it is not");
                    return false;
                }
                Log.LogMessage(MessageImportance.High, $"{mode} Mode - TaskEnvironment is isolated from global environment (PASS)");
            }
            else
            {
                // TaskEnvironment and Environment.GetEnvironmentVariable should be the same
                if (taskEnvValue != globalEnvValue)
                {
                    Log.LogError($"{mode} Mode: Expected TaskEnvironment and Environment.GetEnvironmentVariable to be the same, but they differ");
                    return false;
                }
                Log.LogMessage(MessageImportance.High, $"{mode} Mode - TaskEnvironment matches global environment (PASS)");
            }

            return true;
        }
    }

    /// <summary>
    /// Test task that deliberately performs the unresolved-path operations that multi-threaded strict mode
    /// exists to detect.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class StrictModeProbeTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        /// <summary>
        /// What the task should do: <c>WriteRelativeFile</c>, <c>ChangeCurrentDirectory</c> or <c>Nothing</c>.
        /// </summary>
        public string Behavior { get; set; } = "Nothing";

        public override bool Execute()
        {
            switch (Behavior)
            {
                case "WriteRelativeFile":
                    // Deliberately unresolved against the project directory: this is the defect strict mode
                    // is designed to surface.
                    File.WriteAllText("strict-mode-probe.txt", "probe");
                    break;

                case "ChangeCurrentDirectory":
                    Directory.SetCurrentDirectory(Path.GetTempPath());
                    break;
            }

            return true;
        }
    }

    /// <summary>
    /// Integration tests for MSBuild and CallTarget tasks with TaskEnvironment support.
    /// These tests verify that tasks work correctly in both multithreaded and single-threaded scenarios
    /// with proper environment isolation, following the pattern of MSBuildServer_Tests.
    /// </summary>
    public class MSBuildMultithreaded_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;

        public MSBuildMultithreaded_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Theory]
        [InlineData(true, "/m /nodereuse:false /mt")]
        [InlineData(false, "/m /nodereuse:false")]
        public void MSBuildTask_EnvironmentIsolation(bool isMultithreaded, string msbuildArgs)
        {
            string project = $@"
<Project>
    <UsingTask TaskName='EnvironmentIsolationTestTask' AssemblyFile='{typeof(EnvironmentIsolationTestTask).Assembly.Location}' />
    
    <Target Name='Build'>
        <EnvironmentIsolationTestTask IsMultithreadedMode='{isMultithreaded.ToString().ToLower()}' />
    </Target>
</Project>";
            TransientTestFile projectFile = _env.CreateFile("main.proj", project);
            
            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" {msbuildArgs}",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that MSBUILDFORCEMULTITHREADED=1 propagates all the way to
        /// BuildParameters.MultiThreaded so tasks observe true multi-threaded behavior,
        /// even without the -mt switch on the command line.
        /// </summary>
        [Fact]
        public void MSBuildForceMultiThreadedEnvironmentVariablePropagatesToBuildParameters()
        {
            string project = $@"
<Project>
    <UsingTask TaskName='EnvironmentIsolationTestTask' AssemblyFile='{typeof(EnvironmentIsolationTestTask).Assembly.Location}' />

    <Target Name='Build'>
        <EnvironmentIsolationTestTask IsMultithreadedMode='true' />
    </Target>
</Project>";
            TransientTestFile projectFile = _env.CreateFile("main.proj", project);

            // Set MSBUILDFORCEMULTITHREADED=1 in the env that the spawned MSBuild process inherits,
            // and intentionally do NOT pass /mt on the command line.
            _env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "1");

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" /m /nodereuse:false",
                out bool success,
                false,
                _output);

            // If the env var really propagated to BuildParameters.MultiThreaded, the task
            // observes TaskEnvironment isolated from the global environment (multi-threaded
            // semantics) and the build succeeds.
            success.ShouldBeTrue();
        }

        /// <summary>
        /// Strict mode must not disturb a build whose tasks resolve their paths correctly.
        /// </summary>
        [Fact]
        public void StrictMode_WellBehavedTaskStillSucceeds()
        {
            string output = RunStrictModeProbe("Nothing", "/m /nodereuse:false /mt:strict", out bool success);

            success.ShouldBeTrue(output);
        }

        /// <summary>
        /// A relative path that is never resolved against the project directory writes into the sentinel
        /// current directory, which strict mode detects and reports as MSB4287.
        /// </summary>
        [Fact]
        public void StrictMode_DetectsWriteThroughUnresolvedRelativePath()
        {
            string output = RunStrictModeProbe("WriteRelativeFile", "/m /nodereuse:false /mt:strict", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4287");
            output.ShouldContain("strict-mode-probe.txt");
        }

        /// <summary>
        /// Changing the process current directory corrupts path resolution for every project building in the
        /// process, so strict mode reports it as MSB4286.
        /// </summary>
        [Fact]
        public void StrictMode_DetectsCurrentDirectoryChange()
        {
            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt:strict", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
        }

        /// <summary>
        /// Without the opt-in, the same task must build exactly as before - the whole point of the switch is
        /// that it changes nothing until it is asked for.
        /// </summary>
        [Fact]
        public void StrictMode_IsOptIn()
        {
            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt", out bool success);

            success.ShouldBeTrue(output);
            output.ShouldNotContain("MSB4286");
        }

        /// <summary>
        /// The project is normally named relative to the launch directory, which strict mode moves away from.
        /// </summary>
        [Fact]
        public void StrictMode_BuildsProjectGivenByRelativePath()
        {
            string output = RunStrictModeProbe(
                "Nothing",
                "/m /nodereuse:false /mt:strict",
                out bool success,
                useRelativeProjectPath: true);

            success.ShouldBeTrue(output);
        }

        /// <summary>
        /// A task that declares ContinueOnError has its errors reported as warnings, and strict mode must follow
        /// that contract - otherwise the log claims "Build succeeded" next to an error count.
        /// </summary>
        [Fact]
        public void StrictMode_HonorsContinueOnError()
        {
            string output = RunStrictModeProbe(
                "WriteRelativeFile",
                "/m /nodereuse:false /mt:strict",
                out bool success,
                continueOnError: true);

            success.ShouldBeTrue(output);
            output.ShouldContain("MSB4287");
            output.ShouldContain("0 Error(s)");

            // The engine failed the task, so the "task returned false but did not log an error" diagnostic must
            // not also fire - the task returned true.
            output.ShouldNotContain("MSB4181");
        }

        /// <summary>
        /// The environment variable accepts "true" as well as "1", matching the other opt-in traits.
        /// </summary>
        [Fact]
        public void StrictMode_EnvironmentVariableAcceptsTrue()
        {
            _env.SetEnvironmentVariable("MSBUILDMULTITHREADEDSTRICT", "true");

            string output = RunStrictModeProbe("WriteRelativeFile", "/m /nodereuse:false /mt", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4287");
        }

        private string RunStrictModeProbe(string behavior, string msbuildArgs, out bool success, bool useRelativeProjectPath = false, bool continueOnError = false)
        {
            string project = $"""
                <Project>
                    <UsingTask TaskName="StrictModeProbeTask" AssemblyFile="{typeof(StrictModeProbeTask).Assembly.Location}" />

                    <Target Name="Build">
                        <StrictModeProbeTask Behavior="{behavior}" ContinueOnError="{continueOnError.ToString().ToLowerInvariant()}" />
                    </Target>
                </Project>
                """;

            TransientTestFile projectFile = _env.CreateFile("main.proj", project);

            if (useRelativeProjectPath)
            {
                _env.SetCurrentDirectory(Path.GetDirectoryName(projectFile.Path));

                return RunnerUtilities.ExecMSBuild(
                    BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                    $"main.proj {msbuildArgs}",
                    out success,
                    false,
                    _output);
            }

            return RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" {msbuildArgs}",
                out success,
                false,
                _output);
        }
    }
}
