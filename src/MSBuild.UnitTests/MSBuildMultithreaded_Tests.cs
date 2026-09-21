// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
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

        public override bool Execute()
        {
            int executionCount = Interlocked.Increment(ref s_executionCount);
            Log.LogMessage(MessageImportance.High, $"TaskHostProcessId={EnvironmentUtilities.CurrentProcessId}; ExecutionCount={executionCount}");

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
    /// Test task that deliberately performs the unresolved-path operations that multi-threaded strict mode
    /// exists to detect.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class StrictModeProbeTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        /// <summary>
        /// Selects the deliberate process-directory or unresolved-path behavior to exercise.
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

                case "ReadRelativeFile":
                    Log.LogMessage(MessageImportance.High, $"STRICT-MODE-PROBE-CONTENTS={File.ReadAllText("strict-mode-probe.txt")}");
                    break;

                case "DeleteRelativeFile":
                    File.Delete("strict-mode-probe.txt");
                    break;

                case "ChangeCurrentDirectory":
                    Directory.SetCurrentDirectory(Path.GetTempPath());
                    break;

                case "DeleteSentinel":
                case "BlockSentinelRecovery":
                    string sentinel = Directory.GetCurrentDirectory();
                    if (Path.GetFileName(sentinel) != "MT-sentinel-CWD")
                    {
                        throw new InvalidOperationException("The probe must run in the strict sentinel directory.");
                    }
                    Directory.SetCurrentDirectory(Path.GetDirectoryName(sentinel)!);
                    Directory.Delete(sentinel);
                    if (Behavior == "BlockSentinelRecovery")
                    {
                        File.WriteAllText(sentinel, "occupied");
                    }
                    break;

                case "WriteManyFiles":
                    for (int i = 0; i < 30; i++)
                    {
                        File.WriteAllText($"strict-probe-{i:D2}.txt", "probe");
                    }
                    break;

                case "CaseDistinctDirectory":
                    string currentDirectory = Directory.GetCurrentDirectory();
                    if (Path.GetFileName(currentDirectory) != "MT-sentinel-CWD")
                    {
                        throw new InvalidOperationException("The probe must run in the strict sentinel directory.");
                    }
                    string sibling = Path.Combine(Path.GetDirectoryName(currentDirectory)!, "mt-sentinel-cwd");
                    Directory.CreateDirectory(sibling);
                    Directory.SetCurrentDirectory(sibling);
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
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        public static bool FileSystemIsCaseSensitive => FileUtilities.IsFileSystemCaseSensitive;

        [Theory]
        [InlineData(true, "/m /nodereuse:false /mt")]
        [InlineData(false, "/m /nodereuse:false")]
        public void MSBuildTask_EnvironmentIsolation(bool isMultithreaded, string msbuildArgs)
            => VerifyEnvironmentIsolation(isMultithreaded, msbuildArgs);

        private void VerifyEnvironmentIsolation(bool isMultithreaded, string msbuildArgs)
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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ReusedTaskHostDiscardsOutputFromCachedWriter(bool retainConnection, bool replacePooledProcess)
        {
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            _env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", retainConnection ? null : ChangeWaves.Wave18_12.ToString());
#if NET
            RunnerUtilities.ApplyDotnetHostPathEnvironmentVariable(_env);
#endif
            string project = $"""
                <Project>
                    <UsingTask TaskName="CachedConsoleWriterTestTask" AssemblyFile="{typeof(CachedConsoleWriterTestTask).Assembly.Location}" />
                    <UsingTask TaskName="ProcessIdTask" AssemblyFile="{typeof(ProcessIdTask).Assembly.Location}" />

                    <Target Name="Build">
                        <ProcessIdTask>
                            <Output TaskParameter="Pid" PropertyName="OwnerPid" />
                        </ProcessIdTask>
                        <Message Importance="High" Text="OwnerProcessId=$(OwnerPid);" />
                        <CachedConsoleWriterTestTask />
                    </Target>
                </Project>
                """;
            TransientTestFile projectFile = _env.CreateFile("cached-console-writer.proj", project);
            string arguments = $"\"{projectFile.Path}\" /m:2 /mt /nodereuse:true";

            string firstOutput = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                arguments,
                out bool firstBuildSucceeded,
                false,
                _output);

            firstBuildSucceeded.ShouldBeTrue(firstOutput);
            firstOutput.ShouldContain("ExecutionCount=1");
            firstOutput.ShouldContain("Output through current writer 1");
            int ownerProcessId = ParseProcessId(firstOutput, "OwnerProcessId=");
            _env.WithTransientProcess(ownerProcessId);
            int taskHostProcessId = ParseProcessId(firstOutput, "TaskHostProcessId=");
            _env.WithTransientProcess(taskHostProcessId);

            if (replacePooledProcess)
            {
                using Process process = Process.GetProcessById(taskHostProcessId);
                process.Kill();
                process.WaitForExit(10_000).ShouldBeTrue();
            }

            string secondOutput = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                arguments,
                out bool secondBuildSucceeded,
                false,
                _output);

            secondBuildSucceeded.ShouldBeTrue(secondOutput);
            int secondTaskHostProcessId = ParseProcessId(secondOutput, "TaskHostProcessId=");
            if (secondTaskHostProcessId != taskHostProcessId)
            {
                _env.WithTransientProcess(secondTaskHostProcessId);
            }

            if (retainConnection)
            {
                secondTaskHostProcessId.ShouldBe(taskHostProcessId);
            }

            if (replacePooledProcess)
            {
                secondTaskHostProcessId.ShouldNotBe(taskHostProcessId);
            }

            int expectedExecutionCount = secondTaskHostProcessId == taskHostProcessId ? 2 : 1;
            secondOutput.ShouldContain($"ExecutionCount={expectedExecutionCount}");
            secondOutput.ShouldContain($"Output through current writer {expectedExecutionCount}");
            secondOutput.ShouldNotContain("Output through stale cached writer");
            ParseProcessId(secondOutput, "OwnerProcessId=").ShouldBe(ownerProcessId);
        }

        private static int ParseProcessId(string output, string prefix)
        {
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

        /// <summary>
        /// Strict mode must not disturb a build whose tasks resolve their paths correctly.
        /// </summary>
        [Theory]
        [InlineData("/mt")]
        [InlineData("/mt:true")]
        [InlineData("/multithreaded")]
        public void StrictMode_WellBehavedTaskStillSucceeds(string mtArgument)
        {
            string output = RunStrictModeProbe("Nothing", $"/m /nodereuse:false {mtArgument}", out bool success);

            success.ShouldBeTrue(output);
        }

        /// <summary>
        /// A relative path that is never resolved against the project directory writes into the sentinel
        /// current directory. Even a last-task write must be reported as MSB4287 at project completion.
        /// </summary>
        [Theory]
        [InlineData("/mt")]
        [InlineData("/mt:true")]
        [InlineData("/multithreaded")]
        public void StrictMode_DetectsWriteThroughUnresolvedRelativePath(string mtArgument)
        {
            string output = RunStrictModeProbe("WriteRelativeFile", $"/m /nodereuse:false {mtArgument}", out bool success);

            success.ShouldBeTrue(output);
            output.ShouldContain("MSB4287");
            output.ShouldContain("strict-mode-probe.txt");
        }

        /// <summary>
        /// Changing the process current directory corrupts path resolution for every project building in the
        /// process, so strict mode reports it as MSB4286.
        /// </summary>
        [Theory]
        [InlineData("/mt")]
        [InlineData("/mt:true")]
        [InlineData("/multithreaded")]
        public void StrictMode_DetectsCurrentDirectoryChange(string mtArgument)
        {
            string output = RunStrictModeProbe("ChangeCurrentDirectory", $"/m /nodereuse:false {mtArgument}", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
        }

        /// <summary>
        /// Non-MT builds retain their existing behavior.
        /// </summary>
        [Fact]
        public void StrictMode_IsDisabledOutsideMultiThreadedMode()
        {
            _env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", null);
            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt:false", out bool success);

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
                "/m /nodereuse:false /mt",
                out bool success,
                useRelativeProjectPath: true);

            success.ShouldBeTrue(output);
        }

        [Fact]
        public void StrictMode_WritesOutputCacheRelativeToLaunchDirectory()
        {
            TransientTestFolder launchDirectory = _env.CreateFolder();
            _env.CreateFile(launchDirectory, "main.proj", """
                <Project>
                    <Target Name="Build" />
                </Project>
                """);
            _env.SetCurrentDirectory(launchDirectory.Path);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                "main.proj /m:1 /mt /nr:false /orc:out.cache",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue(output);
            File.Exists(Path.Combine(launchDirectory.Path, "out.cache")).ShouldBeTrue(output);
        }

        /// <summary>
        /// Sentinel contents are reported at project completion, outside the writing task's error policy.
        /// </summary>
        [Fact]
        public void StrictMode_ProjectCompletionWarningIsIndependentOfContinueOnError()
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            string output = RunStrictModeProbe(
                "WriteRelativeFile",
                $"/m:1 /nodereuse:false /mt /bl:\"{binlog.Path}\"",
                out bool success,
                continueOnError: true);

            success.ShouldBeTrue(output);
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            AssertProjectCompletionWarning(logger);
        }

        [Theory]
        [InlineData("", true, 1, 0)]
        [InlineData("/warnAsError:MSB4287", false, 0, 1)]
        [InlineData("/warnAsError", false, 0, 1)]
        [InlineData("/warnAsError /warnNotAsError:MSB4287", true, 1, 0)]
        [InlineData("/warnAsError /nowarn:MSB4287", true, 0, 0)]
        [InlineData("/p:MSBuildWarningsAsErrors=MSB4287", false, 0, 1)]
        [InlineData("/p:MSBuildWarningsAsMessages=MSB4287", true, 0, 0)]
        public void StrictMode_RelativeWriteHonorsWarningControls(string warningArguments, bool succeeds, int warnings, int errors)
            => AssertStrictWarningPolicy("WriteRelativeFile", "MSB4287", warningArguments, succeeds, warnings, errors);

        [Theory]
        [InlineData("", true, 1, 0)]
        [InlineData("/warnAsError:MSB4288", false, 0, 1)]
        [InlineData("/warnAsError", false, 0, 1)]
        [InlineData("/warnAsError /warnNotAsError:MSB4288", true, 1, 0)]
        [InlineData("/warnAsError /nowarn:MSB4288", true, 0, 0)]
        [InlineData("/p:MSBuildWarningsAsErrors=MSB4288", false, 0, 1)]
        [InlineData("/p:MSBuildWarningsAsMessages=MSB4288", true, 0, 0)]
        public void StrictMode_SentinelRecoveryHonorsWarningControls(string warningArguments, bool succeeds, int warnings, int errors)
            => AssertStrictWarningPolicy("DeleteSentinel", "MSB4288", warningArguments, succeeds, warnings, errors);

        private void AssertStrictWarningPolicy(string behavior, string code, string warningArguments, bool succeeds, int warnings, int errors)
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            string output = RunStrictModeProbe(
                behavior,
                $"/m:1 /mt /nr:false /bl:\"{binlog.Path}\" {warningArguments}",
                out bool success);

            success.ShouldBe(succeeds, output);
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.Warnings.Count.ShouldBe(warnings);
            logger.Errors.Count.ShouldBe(errors);
            logger.Warnings.ShouldAllBe(warning => warning.Code == code);
            logger.Errors.ShouldAllBe(error => error.Code == code);
            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBe(succeeds);
        }

        /// <summary>
        /// Current-directory changes are still detected after each task and honor its ContinueOnError policy.
        /// </summary>
        [Fact]
        public void StrictMode_HonorsContinueOnError()
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            string output = RunStrictModeProbe(
                "ChangeCurrentDirectory",
                $"/m:1 /nodereuse:false /mt /bl:\"{binlog.Path}\"",
                out bool success,
                continueOnError: true);

            success.ShouldBeTrue(output);
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.Errors.ShouldBeEmpty();
            BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
            warning.Code.ShouldBe("MSB4286");
            TaskFinishedEventArgs taskFinished = logger.TaskFinishedEvents.ShouldHaveSingleItem();
            BuildEventContext warningContext = warning.BuildEventContext.ShouldNotBeNull();
            BuildEventContext taskContext = taskFinished.BuildEventContext.ShouldNotBeNull();
            warningContext.TaskId.ShouldBe(taskContext.TaskId);
            warningContext.TaskId.ShouldNotBe(BuildEventContext.InvalidTaskId);
            logger.ProjectFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            output.ShouldNotContain("MSB4181");
        }

        [Fact]
        public void StrictMode_ReportsFilesAfterFollowingTargetsRun()
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            TransientTestFile project = _env.CreateFile("deferred-file-check.proj", $"""
                <Project DefaultTargets="Observe">
                  <UsingTask TaskName="StrictModeProbeTask" AssemblyFile="{typeof(StrictModeProbeTask).Assembly.Location}" />
                  <Target Name="Write">
                    <StrictModeProbeTask Behavior="WriteRelativeFile" />
                  </Target>
                  <Target Name="Observe" DependsOnTargets="Write">
                    <StrictModeProbeTask Behavior="ReadRelativeFile" />
                    <Message Text="STRICT-MODE-FOLLOWING-TASK" Importance="high" />
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{project.Path}\" /m:1 /mt /nr:false /bl:\"{binlog.Path}\"",
                out bool success, false, _output);

            success.ShouldBeTrue(output);
            output.ShouldContain("STRICT-MODE-PROBE-CONTENTS=probe");
            output.ShouldContain("STRICT-MODE-FOLLOWING-TASK");
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.TaskFinishedEvents.Select(task => task.TaskName)
                .ShouldBe([nameof(StrictModeProbeTask), nameof(StrictModeProbeTask), "Message"]);
            logger.TaskFinishedEvents.ShouldAllBe(task => task.Succeeded);
            logger.TargetFinishedEvents.Select(target => target.TargetName).ShouldBe(["Write", "Observe"]);
            logger.TargetFinishedEvents.ShouldAllBe(target => target.Succeeded);
            AssertProjectCompletionWarning(logger);
        }

        /// <summary>
        /// Files removed before project completion intentionally escape the deferred sentinel-content scan.
        /// </summary>
        [Fact]
        public void StrictMode_AllowsFilesDeletedBeforeProjectCompletion()
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            TransientTestFile project = _env.CreateFile("deleted-before-completion.proj", $"""
                <Project DefaultTargets="CleanUp">
                  <UsingTask TaskName="StrictModeProbeTask" AssemblyFile="{typeof(StrictModeProbeTask).Assembly.Location}" />
                  <Target Name="Write">
                    <StrictModeProbeTask Behavior="WriteRelativeFile" />
                  </Target>
                  <Target Name="CleanUp" DependsOnTargets="Write">
                    <StrictModeProbeTask Behavior="ReadRelativeFile" />
                    <StrictModeProbeTask Behavior="DeleteRelativeFile" />
                    <Message Text="STRICT-MODE-FILE-DELETED" Importance="high" />
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{project.Path}\" /m:1 /mt /nr:false /bl:\"{binlog.Path}\"",
                out bool success, false, _output);

            success.ShouldBeTrue(output);
            output.ShouldContain("STRICT-MODE-PROBE-CONTENTS=probe");
            output.ShouldContain("STRICT-MODE-FILE-DELETED");
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.Errors.ShouldBeEmpty();
            logger.Warnings.ShouldBeEmpty();
            logger.TaskFinishedEvents.Select(task => task.TaskName)
                .ShouldBe([nameof(StrictModeProbeTask), nameof(StrictModeProbeTask), nameof(StrictModeProbeTask), "Message"]);
            logger.TaskFinishedEvents.ShouldAllBe(task => task.Succeeded);
            logger.ProjectFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Disabling wave 18.12 turns off strict checks without disabling MT itself.
        /// </summary>
        [Theory]
        [InlineData(null, true)]
        [InlineData("18.12", false)]
        [InlineData("999.999", true)]
        public void StrictMode_ChangeWaveControlsChecksAndPreservesMt(string? disabledWave, bool strictModeEnabled)
        {
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);

            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt", out bool success);

            success.ShouldBe(!strictModeEnabled, output);
            output.Contains("MSB4286").ShouldBe(strictModeEnabled, output);
            VerifyEnvironmentIsolation(true, "/m /nodereuse:false /mt");
        }

        [Theory]
        [InlineData("MSBUILDENABLEMULTITHREADED", "", null, true)]
        [InlineData("MSBUILDENABLEMULTITHREADED", "", "18.12", false)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "", null, true)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "", "18.12", false)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "/mt:false", null, true)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "/mt:false", "18.12", false)]
        public void StrictMode_EnvironmentSelectedMtHonorsChangeWave(string mtVariable, string mtArgument, string? disabledWave, bool strictModeEnabled)
        {
            _env.SetEnvironmentVariable("MSBUILDENABLEMULTITHREADED", null);
            _env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", null);
            _env.SetEnvironmentVariable(mtVariable, "1");
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);

            string output = RunStrictModeProbe("ChangeCurrentDirectory", $"/m /nodereuse:false {mtArgument}", out bool success);

            success.ShouldBe(!strictModeEnabled, output);
            output.Contains("MSB4286").ShouldBe(strictModeEnabled, output);
        }

        [Fact]
        public void StrictMode_RepeatedDirectoryChangeDoesNotInheritEarlierWarningPolicy()
        {
            var project = _env.CreateFile("repeated-directory-change.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictModeProbeTask" AssemblyFile="{typeof(StrictModeProbeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictModeProbeTask Behavior="ChangeCurrentDirectory" ContinueOnError="WarnAndContinue" />
                    <StrictModeProbeTask Behavior="ChangeCurrentDirectory" />
                    <Message Text="UNEXPECTED-CONTINUATION" Importance="high" />
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"\"{project.Path}\" /m:1 /mt /nr:false",
                out bool success, false, _output);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
            output.ShouldContain("1 Warning(s)");
            output.ShouldContain("1 Error(s)");
            output.ShouldNotContain("UNEXPECTED-CONTINUATION");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StrictMode_DeletedSentinelIsRecovered(bool continueOnError)
        {
            string output = RunStrictModeProbe("DeleteSentinel", "/m /mt /nr:false", out bool success, continueOnError: continueOnError);

            success.ShouldBeTrue(output);
            output.ShouldContain("MSB4288");
            output.ShouldContain("MT-sentinel-CWD");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StrictMode_RecoveryFailureCannotBeSuppressed(bool continueOnError)
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            string output = RunStrictModeProbe(
                "BlockSentinelRecovery", $"/m /mt /nr:false /nowarn:MSB4288,MSB4290 /bl:\"{binlog.Path}\"",
                out bool success, continueOnError: continueOnError);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4290");
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.Errors.ShouldNotBeEmpty();
            logger.Errors.ShouldAllBe(error => error.Code == "MSB4290");
            logger.Warnings.ShouldBeEmpty();
        }

        [Fact]
        public void StrictMode_AllUnexpectedFilesAreReported()
        {
            string output = RunStrictModeProbe("WriteManyFiles", "/m /mt /nr:false", out bool success);

            success.ShouldBeTrue(output);
            output.ShouldContain("MSB4287");
            for (int i = 0; i < 30; i++)
            {
                output.ShouldContain($"strict-probe-{i:D2}.txt");
            }
        }

        [Fact(Skip = "Requires a case-sensitive file system.", SkipUnless = nameof(FileSystemIsCaseSensitive))]
        public void StrictMode_CaseDistinctDirectoryCannotBypassChecks()
        {
            string output = RunStrictModeProbe("CaseDistinctDirectory", "/m /mt /nr:false", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
        }

        private MockLogger ReadBinlog(string binlogPath)
        {
            var logger = new MockLogger(_output);
            var replay = new BinaryLogReplayEventSource();
            logger.Initialize(replay);
            replay.Replay(binlogPath);
            logger.Shutdown();
            return logger;
        }

        private static void AssertProjectCompletionWarning(MockLogger logger)
        {
            BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
            warning.Code.ShouldBe("MSB4287");
            string message = warning.Message.ShouldNotBeNull();
            message.ShouldContain("strict-mode-probe.txt");
            message.ShouldContain("MT-sentinel-CWD");
            message.ShouldNotContain(nameof(StrictModeProbeTask));
            BuildEventContext warningContext = warning.BuildEventContext.ShouldNotBeNull();
            warningContext.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
            warningContext.TargetId.ShouldBe(BuildEventContext.InvalidTargetId);
            logger.Errors.ShouldBeEmpty();

            ProjectFinishedEventArgs projectFinished = logger.ProjectFinishedEvents.ShouldHaveSingleItem();
            projectFinished.Succeeded.ShouldBeTrue();
            BuildEventContext projectContext = projectFinished.BuildEventContext.ShouldNotBeNull();
            warningContext.ProjectContextId.ShouldBe(projectContext.ProjectContextId);
            warningContext.NodeId.ShouldBe(projectContext.NodeId);
            BuildFinishedEventArgs buildFinished = logger.BuildFinishedEvents.ShouldHaveSingleItem();
            buildFinished.Succeeded.ShouldBeTrue();

            int warningIndex = logger.AllBuildEvents.IndexOf(warning);
            warningIndex.ShouldBeGreaterThan(logger.AllBuildEvents.IndexOf(logger.TargetFinishedEvents.Last()));
            warningIndex.ShouldBeLessThan(logger.AllBuildEvents.IndexOf(projectFinished));
            logger.AllBuildEvents.IndexOf(projectFinished).ShouldBeLessThan(logger.AllBuildEvents.IndexOf(buildFinished));
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
