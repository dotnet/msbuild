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
using Xunit.Abstractions;

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
    }
}
