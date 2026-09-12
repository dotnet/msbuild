// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
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

    public class ConsoleOutputTestTask : Task
    {
        public bool ShouldRunInTaskHost { get; set; }

        public override bool Execute()
        {
            bool isTaskHost = Environment.CommandLine.IndexOf("/nodemode:2", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isTaskHost != ShouldRunInTaskHost)
            {
                Log.LogError($"Expected task host: {ShouldRunInTaskHost}; actual: {isTaskHost}");
                return false;
            }

            Log.LogMessage(MessageImportance.High, "ConsoleOutputTestTask executed");
            Console.WriteLine("ConsoleOutputTestTask output");
            Console.Error.WriteLine("ConsoleOutputTestTask error output");
            return true;
        }
    }

    public class CachedConsoleWriterTestTask : Task
    {
        // Models Spectre.Console caching Console.Out for the lifetime of a reused task-host process.
        private static TextWriter? s_firstConsoleOut;
        private static int s_executionCount;

        public int ExpectedExecutionCount { get; set; }

        public override bool Execute()
        {
            int executionCount = Interlocked.Increment(ref s_executionCount);
            Log.LogMessage(MessageImportance.High, $"TaskHostProcessId={EnvironmentUtilities.CurrentProcessId}; ExecutionCount={executionCount}");

            if (executionCount != ExpectedExecutionCount)
            {
                Log.LogError($"Expected execution count {ExpectedExecutionCount}; actual {executionCount}");
                return false;
            }

            if (s_firstConsoleOut is null)
            {
                s_firstConsoleOut = Console.Out;
            }
            else
            {
                s_firstConsoleOut.WriteLine("Output through stale cached writer");
            }

            Console.WriteLine($"Output through current writer {executionCount}");
            return true;
        }
    }

    public class ExplicitTaskHostConsoleOutputTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "ExplicitTaskHostConsoleOutputTestTask executed");
            Console.WriteLine("EXPLICIT-TASKHOST-STDOUT");
            Console.Error.WriteLine("EXPLICIT-TASKHOST-STDERR");
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
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
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

        [Theory]
        [InlineData(false, "/m:2 /nodereuse:false")]
        [InlineData(true, "/m:2 /nodereuse:false /mt")]
        public void TaskConsoleOutputIsVisible(bool shouldRunInTaskHost, string msbuildArgs)
        {
            string project = $"""
                <Project>
                    <UsingTask TaskName="ConsoleOutputTestTask" AssemblyFile="{typeof(ConsoleOutputTestTask).Assembly.Location}" />

                    <Target Name="Build">
                        <ConsoleOutputTestTask ShouldRunInTaskHost="{shouldRunInTaskHost}" />
                    </Target>
                </Project>
                """;
            TransientTestFile projectFile = _env.CreateFile("console-output.proj", project);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" {msbuildArgs}",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue(output);
            output.ShouldContain("ConsoleOutputTestTask output");
            output.ShouldContain("ConsoleOutputTestTask error output");
        }

        [Fact]
        public void ConsoleOutputFromExplicitTaskHostIsNotForwarded()
        {
            string project = $"""
                <Project>
                    <UsingTask
                        TaskName="ConsoleOutputTestTask"
                        AssemblyFile="{typeof(ConsoleOutputTestTask).Assembly.Location}"
                        TaskFactory="TaskHostFactory"
                        Runtime="{XMakeAttributes.GetCurrentMSBuildRuntime()}"
                        Architecture="{XMakeAttributes.GetCurrentMSBuildArchitecture()}" />

                    <Target Name="Build">
                        <ConsoleOutputTestTask ShouldRunInTaskHost="true" />
                    </Target>
                </Project>
                """;
            TransientTestFile projectFile = _env.CreateFile("explicit-taskhost-console-output.proj", project);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" /m:2 /nodereuse:false /mt",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue(output);
            output.ShouldContain("ConsoleOutputTestTask executed");
            output.ShouldNotContain("ConsoleOutputTestTask output");
            output.ShouldNotContain("ConsoleOutputTestTask error output");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConsoleForwardingDoesNotLeakBetweenSharedTaskHostConfigurations(bool explicitTaskRunsFirst)
        {
            string tasks = explicitTaskRunsFirst
                ? """
                    <ExplicitTaskHostConsoleOutputTestTask />
                    <ConsoleOutputTestTask ShouldRunInTaskHost="true" />
                    """
                : """
                    <ConsoleOutputTestTask ShouldRunInTaskHost="true" />
                    <ExplicitTaskHostConsoleOutputTestTask />
                    """;
            string project = $"""
                <Project>
                    <UsingTask TaskName="ConsoleOutputTestTask" AssemblyFile="{typeof(ConsoleOutputTestTask).Assembly.Location}" />
                    <UsingTask
                        TaskName="ExplicitTaskHostConsoleOutputTestTask"
                        AssemblyFile="{typeof(ExplicitTaskHostConsoleOutputTestTask).Assembly.Location}"
                        TaskFactory="TaskHostFactory"
                        Runtime="{XMakeAttributes.GetCurrentMSBuildRuntime()}"
                        Architecture="{XMakeAttributes.GetCurrentMSBuildArchitecture()}" />

                    <Target Name="Build">
                        {tasks}
                    </Target>
                </Project>
                """;
            TransientTestFile projectFile = _env.CreateFile("mixed-taskhost-console-output.proj", project);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{projectFile.Path}\" /m:2 /nodereuse:false /mt",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue(output);
            output.ShouldContain("ConsoleOutputTestTask output");
            output.ShouldContain("ConsoleOutputTestTask error output");
            output.ShouldContain("ExplicitTaskHostConsoleOutputTestTask executed");
            output.ShouldNotContain("EXPLICIT-TASKHOST-STDOUT");
            output.ShouldNotContain("EXPLICIT-TASKHOST-STDERR");
        }

        [Fact]
        public void ReusedTaskHostDiscardsOutputFromCachedWriter()
        {
            string project = $"""
                <Project>
                    <UsingTask TaskName="CachedConsoleWriterTestTask" AssemblyFile="{typeof(CachedConsoleWriterTestTask).Assembly.Location}" />

                    <Target Name="Build">
                        <CachedConsoleWriterTestTask ExpectedExecutionCount="$(ExpectedExecutionCount)" />
                    </Target>
                </Project>
                """;
            TransientTestFile projectFile = _env.CreateFile("cached-console-writer.proj", project);
            string arguments = $"\"{projectFile.Path}\" /m:2 /mt /nodereuse:true";

            string firstOutput = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"{arguments} /p:ExpectedExecutionCount=1",
                out bool firstBuildSucceeded,
                false,
                _output);

            firstBuildSucceeded.ShouldBeTrue(firstOutput);
            firstOutput.ShouldContain("ExecutionCount=1");
            firstOutput.ShouldContain("Output through current writer 1");
            int taskHostProcessId = ParseTaskHostProcessId(firstOutput);
            _env.WithTransientProcess(taskHostProcessId);

            string secondOutput = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"{arguments} /p:ExpectedExecutionCount=2",
                out bool secondBuildSucceeded,
                false,
                _output);

            secondBuildSucceeded.ShouldBeTrue(secondOutput);
            secondOutput.ShouldContain("ExecutionCount=2");
            secondOutput.ShouldContain("Output through current writer 2");
            secondOutput.ShouldNotContain("Output through stale cached writer");
            ParseTaskHostProcessId(secondOutput).ShouldBe(taskHostProcessId);
        }

        private static int ParseTaskHostProcessId(string output)
        {
            const string prefix = "TaskHostProcessId=";
            int processIdStart = output.IndexOf(prefix, StringComparison.Ordinal);
            processIdStart.ShouldBeGreaterThanOrEqualTo(0);
            processIdStart += prefix.Length;
            int processIdEnd = output.IndexOf(';', processIdStart);
            processIdEnd.ShouldBeGreaterThan(processIdStart);
            return int.Parse(output.Substring(processIdStart, processIdEnd - processIdStart));
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
    }
}
