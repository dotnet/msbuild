// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
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
                    string sentinel = Directory.GetCurrentDirectory();
                    if (Path.GetFileName(sentinel) != "MSBuild-MT-Strict-Sentinel-CWD")
                    {
                        throw new InvalidOperationException("The probe must run in the strict sentinel directory.");
                    }
                    Directory.SetCurrentDirectory(Path.GetDirectoryName(sentinel)!);
                    Directory.Delete(sentinel);
                    break;

                case "WriteManyFiles":
                    for (int i = 0; i < 30; i++)
                    {
                        File.WriteAllText($"strict-probe-{i:D2}.txt", "probe");
                    }
                    break;

                case "CaseDistinctDirectory":
                    string currentDirectory = Directory.GetCurrentDirectory();
                    if (Path.GetFileName(currentDirectory) != "MSBuild-MT-Strict-Sentinel-CWD")
                    {
                        throw new InvalidOperationException("The probe must run in the strict sentinel directory.");
                    }
                    string sibling = Path.Combine(Path.GetDirectoryName(currentDirectory)!, "msbuild-mt-strict-sentinel-cwd");
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
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", null);
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

            success.ShouldBeFalse(output);
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

        /// <summary>
        /// Sentinel contents are checked at project completion, outside the writing task's error policy.
        /// </summary>
        [Fact]
        public void StrictMode_ProjectCompletionErrorIsNotDowngradedByContinueOnError()
        {
            TransientTestFile binlog = _env.CreateFile(".binlog");
            string output = RunStrictModeProbe(
                "WriteRelativeFile",
                $"/m:1 /nodereuse:false /mt /bl:\"{binlog.Path}\"",
                out bool success,
                continueOnError: true);

            success.ShouldBeFalse(output);
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            AssertProjectCompletionError(logger);
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

            success.ShouldBeFalse(output);
            output.ShouldContain("STRICT-MODE-PROBE-CONTENTS=probe");
            output.ShouldContain("STRICT-MODE-FOLLOWING-TASK");
            MockLogger logger = ReadBinlog(binlog.Path);
            logger.TaskFinishedEvents.Select(task => task.TaskName)
                .ShouldBe([nameof(StrictModeProbeTask), nameof(StrictModeProbeTask), "Message"]);
            logger.TaskFinishedEvents.ShouldAllBe(task => task.Succeeded);
            logger.TargetFinishedEvents.Select(target => target.TargetName).ShouldBe(["Write", "Observe"]);
            logger.TargetFinishedEvents.ShouldAllBe(target => target.Succeeded);
            AssertProjectCompletionError(logger);
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
        /// The opt-out does not disable MT itself or require a separate command-line switch.
        /// </summary>
        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        [InlineData("TRUE")]
        public void StrictMode_EnvironmentOptOutPreservesMt(string optOut)
        {
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut);

            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt", out bool success);

            success.ShouldBeTrue(output);
            output.ShouldNotContain("MSB4286");
            VerifyEnvironmentIsolation(true, "/m /nodereuse:false /mt");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("invalid")]
        public void StrictMode_OnlyRecognizedOptOutValuesDisableChecks(string optOut)
        {
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut);

            string output = RunStrictModeProbe("ChangeCurrentDirectory", "/m /nodereuse:false /mt", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
        }

        [Fact]
        public void StrictMode_ProjectPropertyCannotOptOut()
        {
            string output = RunStrictModeProbe(
                "ChangeCurrentDirectory", "/m /nodereuse:false /mt /p:MSBUILDMTNONSTRICT=1", out bool success);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSB4286");
        }

        [Theory]
        [InlineData("MSBUILDENABLEMULTITHREADED", "", false)]
        [InlineData("MSBUILDENABLEMULTITHREADED", "", true)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "", false)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "", true)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "/mt:false", false)]
        [InlineData("MSBUILDFORCEMULTITHREADED", "/mt:false", true)]
        public void StrictMode_EnvironmentSelectedMtHonorsOptOut(string mtVariable, string mtArgument, bool optOut)
        {
            _env.SetEnvironmentVariable("MSBUILDENABLEMULTITHREADED", null);
            _env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", null);
            _env.SetEnvironmentVariable(mtVariable, "1");
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut ? "1" : null);

            string output = RunStrictModeProbe("ChangeCurrentDirectory", $"/m /nodereuse:false {mtArgument}", out bool success);

            success.ShouldBe(optOut, output);
            output.Contains("MSB4286").ShouldBe(!optOut, output);
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
        public void StrictMode_DeletedSentinelCannotBeDowngradedToSuccess(bool continueOnError)
        {
            string output = RunStrictModeProbe("DeleteSentinel", "/m /mt /nr:false", out bool success, continueOnError: continueOnError);

            success.ShouldBeFalse(output);
            output.ShouldContain("MSBuild-MT-Strict-Sentinel-CWD");
        }

        [Fact]
        public void StrictMode_AllUnexpectedFilesAreReported()
        {
            string output = RunStrictModeProbe("WriteManyFiles", "/m /mt /nr:false", out bool success);

            success.ShouldBeFalse(output);
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

        private static void AssertProjectCompletionError(MockLogger logger)
        {
            BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();
            error.Code.ShouldBe("MSB4287");
            string message = error.Message.ShouldNotBeNull();
            message.ShouldContain("strict-mode-probe.txt");
            message.ShouldContain("MSBuild-MT-Strict-Sentinel-CWD");
            message.ShouldNotContain(nameof(StrictModeProbeTask));
            BuildEventContext errorContext = error.BuildEventContext.ShouldNotBeNull();
            errorContext.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
            errorContext.TargetId.ShouldBe(BuildEventContext.InvalidTargetId);
            logger.Warnings.ShouldBeEmpty();

            ProjectFinishedEventArgs projectFinished = logger.ProjectFinishedEvents.ShouldHaveSingleItem();
            projectFinished.Succeeded.ShouldBeFalse();
            BuildEventContext projectContext = projectFinished.BuildEventContext.ShouldNotBeNull();
            errorContext.ProjectContextId.ShouldBe(projectContext.ProjectContextId);
            errorContext.NodeId.ShouldBe(projectContext.NodeId);
            BuildFinishedEventArgs buildFinished = logger.BuildFinishedEvents.ShouldHaveSingleItem();
            buildFinished.Succeeded.ShouldBeFalse();

            int errorIndex = logger.AllBuildEvents.IndexOf(error);
            errorIndex.ShouldBeGreaterThan(logger.AllBuildEvents.IndexOf(logger.TargetFinishedEvents.Last()));
            errorIndex.ShouldBeLessThan(logger.AllBuildEvents.IndexOf(projectFinished));
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
