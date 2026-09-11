// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Logging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the default multi-threaded strict checks described in
    /// https://github.com/dotnet/msbuild/issues/14794.
    /// </summary>
    public class MultiThreadedStrictMode_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;

        public MultiThreadedStrictMode_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", null);
        }

        public void Dispose() => _env.Dispose();

        public static bool FileSystemIsCaseSensitive => FileUtilities.IsFileSystemCaseSensitive;

        [Theory]
        [InlineData(true, null, true)]
        [InlineData(true, "", true)]
        [InlineData(true, "0", true)]
        [InlineData(true, "false", true)]
        [InlineData(true, "False", true)]
        [InlineData(true, "invalid", true)]
        [InlineData(true, "1", false)]
        [InlineData(true, "true", false)]
        [InlineData(true, "TRUE", false)]
        [InlineData(false, null, false)]
        [InlineData(false, "0", false)]
        [InlineData(false, "false", false)]
        [InlineData(false, "1", false)]
        [InlineData(false, "true", false)]
        public void StrictChecksDependOnlyOnMtAndEnvironmentOptOut(bool multiThreaded, string? optOut, bool expectedStrict)
        {
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut);
            string originalDirectory = Directory.GetCurrentDirectory();
            BuildParameters parameters = new()
            {
                MultiThreaded = multiThreaded,
                Loggers = [new MockLogger(_output)],
            };
            using BuildManager manager = new();
            manager.BeginBuild(parameters);

            try
            {
                (MultiThreadedStrictModeScope.ActiveScope is not null).ShouldBe(expectedStrict);
                if (expectedStrict)
                {
                    MultiThreadedStrictModeScope.ActiveScope!.BuildId.ShouldBe(((IBuildComponentHost)manager).BuildParameters.BuildId);
                }
                else
                {
                    Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
                }
            }
            finally
            {
                manager.EndBuild();
            }

            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void ReusedBuildManagerDoesNotRetainPreviousStrictState()
        {
            using BuildManager manager = new();
            string originalDirectory = Directory.GetCurrentDirectory();
            BuildParameters parameters = new() { MultiThreaded = true };
            bool[] optOutValues = [false, true, false];
            foreach (bool optOut in optOutValues)
            {
                _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut ? "1" : null);
                parameters.Loggers = [new MockLogger(_output)];
                manager.BeginBuild(parameters);
                try
                {
                    (MultiThreadedStrictModeScope.ActiveScope is not null).ShouldBe(!optOut);
                }
                finally
                {
                    manager.EndBuild();
                }

                Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            }
        }

        [Fact]
        public void ProductionApiBuildRecapturesStrictOptOut()
        {
            bool runningTests = BuildEnvironmentState.s_runningTests;
            try
            {
                Traits.UpdateFromEnvironment();
                BuildEnvironmentState.s_runningTests = false;
                using BuildManager manager = new();
                string?[] optOutValues = [null, "1", null];
                foreach (string? optOut in optOutValues)
                {
                    _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut);
                    manager.BeginBuild(new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] });
                    try
                    {
                        (MultiThreadedStrictModeScope.ActiveScope is not null).ShouldBe(optOut is null);
                    }
                    finally
                    {
                        manager.EndBuild();
                    }
                }
            }
            finally
            {
                _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", null);
                BuildEnvironmentState.s_runningTests = runningTests;
                Traits.UpdateFromEnvironment();
            }
        }

        [Fact]
        public void NonMtIsolationPreservesHostRelativeDeclarations()
        {
            var projectDirectory = _env.CreateFolder();
            var hostDirectory = _env.CreateFolder();
            var child = _env.CreateFile(hostDirectory, "child.proj", """
                <Project><Target Name="Build" /></Project>
                """);
            var root = _env.CreateFile(projectDirectory, "root.proj", $"""
                <Project>
                  <ItemGroup><ProjectReference Include="child.proj" /></ItemGroup>
                  <Target Name="Build"><MSBuild Projects="{child.Path}" Targets="Build" /></Target>
                </Project>
                """);
            _env.SetCurrentDirectory(hostDirectory.Path);
            using BuildManager manager = new();
            manager.BeginBuild(new BuildParameters
            {
                MultiThreaded = false,
                SaveOperatingEnvironment = false,
                ProjectIsolationMode = ProjectIsolationMode.True,
                DisableInProcNode = false,
                MaxNodeCount = 1,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [new MockLogger(_output)],
            });
            try
            {
                manager.BuildRequest(new BuildRequestData(child.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
                manager.BuildRequest(new BuildRequestData(root.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
            }
            finally
            {
                manager.EndBuild();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ChangingOptOutDuringBuildDoesNotChangeTaskChecks(bool optOut)
        {
            _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut ? "1" : null);
            _env.SetCurrentDirectory(_env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            string otherDirectory = _env.CreateFolder().Path;
            var project = _env.CreateFile("change-opt-out.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ChangeDirectoryOnExecute="true" OtherDirectory="{otherDirectory}" />
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [logger],
            };
            using BuildManager manager = new();
            BuildResult result;
            manager.BeginBuild(parameters);
            try
            {
                _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", optOut ? null : "1");
                result = manager.BuildRequest(new BuildRequestData(
                    project.Path, new Dictionary<string, string?>(), null, ["Build"], null));
            }
            finally
            {
                manager.EndBuild();
            }

            result.OverallResult.ShouldBe(optOut ? BuildResultCode.Success : BuildResultCode.Failure);
            logger.Errors.Exists(e => e.Code == "MSB4286").ShouldBe(!optOut);
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskChecksOnlyUseTheirOwnBuildsScope(bool multiThreaded)
        {
            var project = _env.CreateFile("foreign-scope.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            string originalDirectory = Directory.GetCurrentDirectory();
            using BuildManager owner = new();
            owner.BeginBuild(new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] });
            try
            {
                var scope = MultiThreadedStrictModeScope.ActiveScope.ShouldNotBeNull();
                _env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", "1");
                MockLogger logger = new(_output);
                using BuildManager other = new();
                BuildParameters parameters = new()
                {
                    MultiThreaded = multiThreaded,
                    SaveOperatingEnvironment = false,
                    ShutdownInProcNodeOnBuildFinish = true,
                    EnableNodeReuse = false,
                    Loggers = [logger],
                };

                other.Build(parameters, new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();

                logger.AssertNoErrors();
                logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
                MultiThreadedStrictModeScope.ActiveScope.ShouldBeSameAs(scope);
                scope.DetectViolations().UnresolvedPathWrites.ShouldBe("late-output.txt");
            }
            finally
            {
                owner.EndBuild();
            }

            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        /// <summary>
        /// The scope owns process-wide state, so entering must move the process and exiting must put it back.
        /// </summary>
        [Fact]
        public void ScopeMovesAndRestoresCurrentDirectory()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                MultiThreadedStrictModeScope.ActiveScope.ShouldBe(scope);

                // Compare leaf names: on Unix the directory is entered through a symlinked temporary folder.
                Path.GetFileName(Directory.GetCurrentDirectory())
                    .ShouldBe(MultiThreadedStrictModeScope.SentinelDirectoryName);

                Directory.EnumerateFileSystemEntries(scope.SentinelDirectory).ShouldBeEmpty();
            }
            finally
            {
                scope.Exit();
            }

            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            Directory.Exists(scope.SentinelDirectory).ShouldBeFalse();
            Directory.Exists(Path.GetDirectoryName(scope.SentinelDirectory)).ShouldBeFalse();
        }

        /// <summary>
        /// Only one scope may own the process current directory, and exiting twice must not disturb whoever owns
        /// it next.
        /// </summary>
        [Fact]
        public void SecondScopeIsRejectedAndExitIsIdempotent()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                Should.Throw<InvalidOperationException>(() => MultiThreadedStrictModeScope.Enter(1));
                MultiThreadedStrictModeScope.ActiveScope.ShouldBe(scope);
            }
            finally
            {
                scope.Exit();
                scope.Exit();
            }

            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        /// <summary>
        /// A write through an unresolved relative path lands in the sentinel directory, and must be reported
        /// exactly once so that later tasks are not failed for a file that was already reported.
        /// </summary>
        [Fact]
        public void UnresolvedPathWriteIsDetectedOnceAndRemoved()
        {
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                scope.DetectViolations().Any.ShouldBeFalse();

                // A relative path resolves against the process current directory, which is the whole defect.
                File.WriteAllText("unresolved.txt", "probe");

                MultiThreadedStrictModeScope.Violations violations = scope.DetectViolations();
                violations.UnresolvedPathWrites.ShouldBe("unresolved.txt");
                violations.UnexpectedCurrentDirectory.ShouldBeNull();

                // Removed, so that it cannot satisfy a later task's unresolved read, and not reported again.
                File.Exists(Path.Combine(scope.SentinelDirectory, "unresolved.txt")).ShouldBeFalse();
                scope.DetectViolations().Any.ShouldBeFalse();
            }
            finally
            {
                scope.Exit();
            }
        }

        /// <summary>
        /// Detection has to be deterministic to be worth anything: the whole point of the mode is to remove
        /// load-dependent flakiness, so a stray write must be reported on the very next verification, every time.
        /// </summary>
        [Fact]
        public void UnresolvedPathWriteIsDetectedEveryTime()
        {
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                for (int i = 0; i < 50; i++)
                {
                    string name = $"stray{i}.txt";
                    File.WriteAllText(name, "probe");

                    scope.DetectViolations().UnresolvedPathWrites.ShouldBe(name, $"iteration {i}");
                }
            }
            finally
            {
                scope.Exit();
            }
        }

        [WindowsOnlyFact]
        public void RecreatedPreviouslyLockedEntryIsReportedAgain()
        {
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            const string Name = "reused.txt";
            using (FileStream held = new(Name, FileMode.Create, System.IO.FileAccess.ReadWrite, FileShare.Read))
            {
                scope.DetectViolations().UnresolvedPathWrites.ShouldBe(Name);
                File.Exists(Name).ShouldBeTrue();
            }

            File.Delete(Name);
            scope.DetectViolations().Any.ShouldBeFalse();
            File.WriteAllText(Name, "new output");

            scope.DetectViolations().UnresolvedPathWrites.ShouldBe(Name);
            File.Exists(Name).ShouldBeFalse();
        }

        [WindowsOnlyFact]
        public void PreviouslyReportedEntryIsRetriedOnceUnlocked()
        {
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            const string Name = "unlocked.txt";
            using (FileStream held = new(Name, FileMode.Create, System.IO.FileAccess.ReadWrite, FileShare.Read))
            {
                scope.DetectViolations().UnresolvedPathWrites.ShouldBe(Name);
                scope.DetectViolations().Any.ShouldBeFalse();
            }

            scope.DetectViolations().Any.ShouldBeFalse();
            File.Exists(Name).ShouldBeFalse();
        }

        [Fact]
        public void UnresolvedPathWritesAreReportedAndRemovedInOneCheck()
        {
            const int StrayCount = 25;
            using TestEnvironment env = TestEnvironment.Create(_output);
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            string[] expected = new string[StrayCount];

            for (int i = 0; i < StrayCount; i++)
            {
                expected[i] = $"stray{i:D2}.txt";
                File.WriteAllText(expected[i], "probe");
            }

            string? reported = scope.DetectViolations().UnresolvedPathWrites;
            reported.ShouldNotBeNull();
            reported!.Split([", "], StringSplitOptions.None).ShouldBe(expected, ignoreOrder: true);
            Directory.EnumerateFileSystemEntries(scope.SentinelDirectory).ShouldBeEmpty();
            scope.DetectViolations().Any.ShouldBeFalse();
        }

        [Fact]
        public void CurrentDirectoryChangeIsDetectedOnceAndRepaired()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                Directory.SetCurrentDirectory(originalDirectory);

                MultiThreadedStrictModeScope.Violations violations = scope.DetectViolations();
                violations.UnexpectedCurrentDirectory.ShouldNotBeNull();

                // Repaired, so the rest of the build keeps the protection it asked for.
                Path.GetFileName(Directory.GetCurrentDirectory())
                    .ShouldBe(MultiThreadedStrictModeScope.SentinelDirectoryName);

                scope.DetectViolations().Any.ShouldBeFalse();
            }
            finally
            {
                scope.Exit();
            }

            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void RepeatedChangesToTheSameDirectoryAreDetected()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);

            for (int i = 0; i < 2; i++)
            {
                Directory.SetCurrentDirectory(originalDirectory);
                scope.DetectViolations().UnexpectedCurrentDirectory.ShouldBe(originalDirectory);
                Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
                scope.DetectViolations().Any.ShouldBeFalse();
            }
        }

        [Fact(Skip = "Requires a case-sensitive file system.", SkipUnless = nameof(FileSystemIsCaseSensitive))]
        public void CaseDistinctSiblingDirectoryIsDetectedAndRepaired()
        {
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            string sibling = Path.Combine(Path.GetDirectoryName(scope.SentinelDirectory)!, "msbuild-mt-strict-sentinel-cwd");
            Directory.CreateDirectory(sibling);
            Directory.SetCurrentDirectory(sibling);

            scope.DetectViolations().UnexpectedCurrentDirectory.ShouldBe(sibling);
            Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
        }

        [Fact]
        public void RepeatedDirectoryViolationHonorsEachTasksFailurePolicy()
        {
            string otherDirectory = _env.CreateFolder().Path;
            var project = _env.CreateFile("repeated-directory-change.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ChangeDirectoryOnExecute="true" OtherDirectory="{otherDirectory}" ContinueOnError="WarnAndContinue" />
                    <StrictLifetimeTask ChangeDirectoryOnExecute="true" OtherDirectory="{otherDirectory}" />
                    <Message Text="UNEXPECTED-CONTINUATION" Importance="high" />
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);

            BuildStrictProject(project.Path, logger).ShouldHaveFailed();

            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4286");
            logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("MSB4286");
            logger.TaskFinishedEvents.Count.ShouldBe(2);
            logger.TaskFinishedEvents.ShouldAllBe(e => !e.Succeeded);
            logger.AssertLogDoesntContain("UNEXPECTED-CONTINUATION");
        }

        [Theory]
        [InlineData("ErrorAndStop")]
        [InlineData("WarnAndContinue")]
        public void VerificationFailureDoesNotLogSuccessfulTaskCompletion(string continueOnError)
        {
            _env.SetCurrentDirectory(_env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            var project = _env.CreateFile("verification-failure.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ContinueOnError="{continueOnError}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                    <Message Text="UNEXPECTED-CONTINUATION" Importance="high" />
                  </Target>
                </Project>
                """);
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictLifetimeTask), new OutputCallbackHost(() =>
            {
                string sentinel = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(sentinel);
            }));
            MockLogger logger = new(_output) { AllowTaskCrashes = true };
            BuildResult? result = null;
            Exception? exception = Record.Exception(() => result = BuildStrictProject(project.Path, logger, hostServices: hostServices));

            if (exception is null)
            {
                result.ShouldNotBeNull().ShouldHaveFailed();
            }
            else
            {
                exception.ShouldBeOfType<DirectoryNotFoundException>();
            }

            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
            logger.AssertLogDoesntContain("UNEXPECTED-CONTINUATION");
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        /// <summary>
        /// Strict mode is meaningless outside multi-threaded mode, so a build that is not multi-threaded must not
        /// have the process moved out from under it when strict checks have not been opted out.
        /// </summary>
        [Fact]
        public void StrictModeIsIgnoredWhenBuildIsNotMultiThreaded()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", null);

            string originalDirectory = Directory.GetCurrentDirectory();

            BuildParameters parameters = new()
            {
                MultiThreaded = false,
                Loggers = [new MockLogger(_output)],
            };

            using BuildManager manager = new();
            manager.BeginBuild(parameters);

            try
            {
                MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
                Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            }
            finally
            {
                manager.EndBuild();
            }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void MtBuildRestoresHostDirectory(bool strict, bool saveEnvironment)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", strict ? null : "1");
            string startupDirectory = BuildParameters.StartupDirectory;
            var projectFolder = env.CreateFolder();
            env.CreateFile(projectFolder, "build.proj", """
                <Project>
                  <Target Name="Build"><Message Text="built" /></Target>
                </Project>
                """);
            env.SetCurrentDirectory(projectFolder.Path);
            string hostDirectory = Directory.GetCurrentDirectory();
            hostDirectory.ShouldNotBe(startupDirectory);

            MockLogger logger = new(_output);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                SaveOperatingEnvironment = saveEnvironment,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [logger],
            };

            // Resolve the filename while the host is in B, before project loading resets CWD to A.
            BuildRequestData request = new("build.proj", new Dictionary<string, string?>(), null, ["Build"], null);
            manager.Build(parameters, request).ShouldHaveSucceeded();
            Directory.GetCurrentDirectory().ShouldBe(hostDirectory);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NonStrictMtRespectsSaveOperatingEnvironment(bool saveEnvironment)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", "1");
            env.SetCurrentDirectory(env.CreateFolder().Path);
            string changedDirectory = Directory.GetCurrentDirectory();
            env.SetCurrentDirectory(env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            var project = env.CreateFile("change-directory.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ChangeDirectoryOnExecute="true" OtherDirectory="{changedDirectory}" />
                  </Target>
                </Project>
                """);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                SaveOperatingEnvironment = saveEnvironment,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [new MockLogger(_output)],
            };

            manager.Build(parameters, new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
            Directory.GetCurrentDirectory().ShouldBe(saveEnvironment ? originalDirectory : changedDirectory);
        }

        [WindowsOnlyFact]
        public void ScopeDoesNotReuseLockedLeftovers()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetCurrentDirectory(Directory.GetCurrentDirectory());
            MultiThreadedStrictModeScope first = MultiThreadedStrictModeScope.Enter(0);
            using var firstLifetime = new ScopeLifetime(first);
            string file = Path.Combine(first.SentinelDirectory, "locked.txt");
            using FileStream lockedFile = new(file, FileMode.Create, System.IO.FileAccess.ReadWrite, FileShare.Read);
            first.Exit();

            MultiThreadedStrictModeScope second = MultiThreadedStrictModeScope.Enter(1);
            using var secondLifetime = new ScopeLifetime(second);
            second.SentinelDirectory.ShouldNotBe(first.SentinelDirectory);
            File.Exists(file).ShouldBeTrue();
            File.Exists(Path.Combine(second.SentinelDirectory, "locked.txt")).ShouldBeFalse();
            second.DetectViolations().Any.ShouldBeFalse();
        }

        [Fact]
        public void ScopeHandoffCapturesDirectoryAfterAcquiringOwnership()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetCurrentDirectory(env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            MultiThreadedStrictModeScope first = MultiThreadedStrictModeScope.Enter(0);
            using var firstLifetime = new ScopeLifetime(first);
            MultiThreadedStrictModeScope? second = null;
            Exception? threadException = null;
            using ManualResetEventSlim started = new();
            Thread thread = new(() =>
            {
                started.Set();
                try
                {
                    second = MultiThreadedStrictModeScope.Enter(1);
                }
                catch (Exception e)
                {
                    threadException = e;
                }
            });

            // Hold the actual ownership lock so the entrant must wait until the first scope exits.
            object stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            bool blocked;
            lock (stateLock)
            {
                thread.Start();
                started.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                blocked = SpinWait.SpinUntil(
                    () => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(10));
                first.Exit();
            }

            thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            using var secondLifetime = new ScopeLifetime(second);
            blocked.ShouldBeTrue();
            threadException.ShouldBeNull();
            second.ShouldNotBeNull();
            second.Exit();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Theory]
        [InlineData("Write", "ErrorAndStop", false)]
        [InlineData("Write", "ErrorAndContinue", false)]
        [InlineData("Write", "WarnAndContinue", true)]
        [InlineData("ChangeDirectory", "ErrorAndStop", false)]
        [InlineData("ChangeDirectory", "ErrorAndContinue", false)]
        [InlineData("ChangeDirectory", "WarnAndContinue", true)]
        public void OutputGetterViolationUsesTaskFailurePolicy(string violation, string continueOnError, bool succeeds)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", null);
            var directory = env.CreateFolder();
            var project = env.CreateFile("getter.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="{violation}" OtherDirectory="{directory.Path}" ContinueOnError="{continueOnError}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                    <Message Text="AFTER:$(MSBuildLastTaskResult):$(Value)" Importance="high" />
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            BuildResult result = BuildStrictProject(project.Path, logger);
            result.OverallResult.ShouldBe(succeeds ? BuildResultCode.Success : BuildResultCode.Failure);
            logger.AssertLogContains(violation == "Write" ? "MSB4287" : "MSB4286");
            logger.AssertLogDoesntContain("MSB4181");
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBeFalse();
            if (continueOnError == "ErrorAndStop")
            {
                logger.AssertLogDoesntContain("AFTER:");
            }
            else
            {
                logger.AssertLogContains("AFTER:false:value");
            }
        }

        [Theory]
        [InlineData("Write", false)]
        [InlineData("ChangeDirectory", false)]
        [InlineData("Write", true)]
        [InlineData("ChangeDirectory", true)]
        public void FinalTaskOutputGetterViolationIsDetected(string violation, bool returnFalse)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var directory = env.CreateFolder();
            var project = env.CreateFile("last-getter.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="{violation}" OtherDirectory="{directory.Path}" ReturnFalse="{returnFalse}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            BuildStrictProject(project.Path, logger).ShouldHaveFailed();
            string strictDiagnostic = violation == "Write" ? "MSB4287" : "MSB4286";
            logger.Errors.Count.ShouldBe(returnFalse ? 2 : 1);
            logger.Errors[0].Code.ShouldBe(returnFalse ? "MSB4181" : strictDiagnostic);
            logger.Errors[logger.Errors.Count - 1].Code.ShouldBe(strictDiagnostic);
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBeFalse();
        }

        [WindowsFullFrameworkOnlyFact]
        public void StaOutputViolationUsesCompletedTaskResult()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var project = env.CreateFile("sta-getter.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictStaLifetimeTask" AssemblyFile="{typeof(StrictStaLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictStaLifetimeTask Violation="Write" ContinueOnError="WarnAndContinue">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictStaLifetimeTask>
                    <Message Text="STA_RESULT:$(MSBuildLastTaskResult):$(Value)" Importance="high" />
                  </Target>
                </Project>
                """);
            ApartmentState? observedApartment = null;
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictStaLifetimeTask),
                new OutputCallbackHost(() => observedApartment = Thread.CurrentThread.GetApartmentState()));
            MockLogger logger = new(_output);

            BuildStrictProject(project.Path, logger, hostServices: hostServices).ShouldHaveSucceeded();

            observedApartment.ShouldBe(ApartmentState.STA);
            logger.AssertLogContains("MSB4287", "STA_RESULT:false:value");
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictStaLifetimeTask))!.Succeeded.ShouldBeFalse();
        }

        [Theory]
        [InlineData("ErrorAndStop")]
        [InlineData("ErrorAndContinue")]
        [InlineData("WarnAndContinue")]
        public void ThrowingOutputGetterRemainsAHardFailure(string continueOnError)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var project = env.CreateFile("getter-throws.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ThrowFromGetter="true" ContinueOnError="{continueOnError}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                    <Message Text="UNEXPECTED-CONTINUATION" Importance="high" />
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output) { AllowTaskCrashes = true };
            BuildStrictProject(project.Path, logger).ShouldHaveFailed();
            logger.AssertLogContains("MSB4028");
            logger.AssertLogDoesntContain("UNEXPECTED-CONTINUATION");
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBeFalse();
        }

        [Theory]
        [InlineData("Write", "ErrorAndStop", false)]
        [InlineData("Write", "ErrorAndContinue", false)]
        [InlineData("Write", "WarnAndContinue", true)]
        [InlineData("ChangeDirectory", "ErrorAndStop", false)]
        [InlineData("ChangeDirectory", "ErrorAndContinue", false)]
        [InlineData("ChangeDirectory", "WarnAndContinue", true)]
        [InlineData("Throw", "ErrorAndStop", false)]
        [InlineData("Throw", "ErrorAndContinue", false)]
        [InlineData("Throw", "WarnAndContinue", false)]
        public void CleanupViolationUsesTaskFailurePolicy(string violation, string continueOnError, bool succeeds)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var otherDirectory = env.CreateFolder();
            TaskBuilder? builder = null;
            int cleanupCalls = 0;
            Action onOutput = () =>
            {
                // Decorate only after the normal MT task was created. Arbitrary custom factories
                // are not supported in MT; this seam tests the engine's cleanup boundary.
                var host = (TaskExecutionHost)typeof(TaskBuilder)
                    .GetField("_taskExecutionHost", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(builder)!;
                var original = host._UNITTESTONLY_TaskFactoryWrapper;
                host._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(
                    new CleanupCallbackFactory(original.TaskFactory, () =>
                    {
                        cleanupCalls++;
                        switch (violation)
                        {
                            case "Write":
                                File.WriteAllText("cleanup-only.txt", "value");
                                break;
                            case "ChangeDirectory":
                                Directory.SetCurrentDirectory(otherDirectory.Path);
                                break;
                            case "Throw":
                                throw new InvalidProjectFileException("cleanup-failure");
                        }
                    }),
                    original.TaskFactoryLoadedType, nameof(StrictLifetimeTask),
                    original.FactoryIdentityParameters, original.Statistics);
            };

            var project = env.CreateFile("cleanup.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ContinueOnError="{continueOnError}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictLifetimeTask), new OutputCallbackHost(onOutput));
            BuildResult result = BuildStrictProject(project.Path, logger, manager =>
                ((IBuildComponentHost)manager).RegisterFactory(BuildComponentType.TaskBuilder,
                    type => builder = (TaskBuilder)TaskBuilder.CreateComponent(type)), hostServices);

            cleanupCalls.ShouldBe(1);
            result.OverallResult.ShouldBe(succeeds ? BuildResultCode.Success : BuildResultCode.Failure);
            logger.AssertLogContains(violation switch { "Write" => "MSB4287", "ChangeDirectory" => "MSB4286", _ => "cleanup-failure" });
            logger.AssertLogDoesntContain("MSB4181");
            logger.TaskFinishedEvents.Count.ShouldBe(1);
            logger.TaskFinishedEvents[0].Succeeded.ShouldBeFalse();
            logger.ErrorCount.ShouldBe(succeeds ? 0 : 1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancellationDuringOutputGatheringPreservesEarlierDiagnostics(bool strict)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMTNONSTRICT", strict ? null : "1");
            var otherDirectory = env.CreateFolder();
            var project = env.CreateFile("cancel-getter.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask ReturnFalse="true" ChangeDirectoryOnExecute="true" OtherDirectory="{otherDirectory.Path}">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            using BuildManager manager = new();
            TaskBuilder? builder = null;
            ((IBuildComponentHost)manager).RegisterFactory(BuildComponentType.TaskBuilder,
                type => builder = (TaskBuilder)TaskBuilder.CreateComponent(type));
            bool cancellationObserved = false;
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictLifetimeTask), new OutputCallbackHost(() =>
            {
                manager.CancelAllSubmissions();
                var token = (CancellationToken)typeof(TaskBuilder)
                    .GetField("_cancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(builder)!;
                cancellationObserved = SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(10));
            }));
            MockLogger logger = new(_output);

            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [logger],
                HostServices = hostServices,
            };
            BuildResult result = manager.Build(parameters,
                new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], hostServices));

            cancellationObserved.ShouldBeTrue();
            result.ShouldHaveFailed();
            logger.AssertLogContains("MSB4181");
            logger.AssertLogDoesntContain("MSB4286");
            logger.AssertLogDoesntContain("MSB4287");
        }

        [Fact]
        public void StrictSetupFailureDoesNotBecomeAnInactiveBuild()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string originalDirectory = Directory.GetCurrentDirectory();
            var temp = env.CreateFolder();
            env.SetTempPath(temp.Path);
            Directory.Delete(temp.Path);
            using FileStream invalidTempRoot = new(
                temp.Path, FileMode.Create, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete,
                1, FileOptions.DeleteOnClose);

            Should.Throw<IOException>(() => MultiThreadedStrictModeScope.Enter(0));
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void StrictVerificationFailureDoesNotLookClean()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string originalDirectory = Directory.GetCurrentDirectory();
            env.SetCurrentDirectory(originalDirectory);
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(scope.SentinelDirectory);

            Should.Throw<DirectoryNotFoundException>(() => scope.DetectViolations());
        }

        [Fact]
        public void FailedStrictEntryLeavesBuildManagerReusable()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var project = env.CreateFile("retry-entry.proj", """
                <Project><Target Name="Build"><Message Text="retry succeeded" /></Target></Project>
                """);
            var first = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            string outputCache = Path.Combine(env.CreateFolder().Path, "failed-entry.cache");
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [new MockLogger(_output)],
                OutputResultsCacheFile = outputCache,
            };

            Exception? exception = Record.Exception(() => manager.BeginBuild(parameters));
            if (exception is null)
            {
                manager.EndBuild();
            }

            exception.ShouldBeOfType<InvalidOperationException>();
            File.Exists(outputCache).ShouldBeFalse();
            first.Exit();
            parameters.Loggers = [new MockLogger(_output)];
            manager.Build(parameters, new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
        }

        [Fact]
        public void FailedStrictEntryPreservesEntryAndShutdownExceptions()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var first = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            LoggerException shutdownFailure = new("logger shutdown failure");
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                Loggers =
                [
                    new MockLogger(_output),
                    new LoggingService_Tests.LoggerThrowException(true, false, shutdownFailure),
                ],
            };

            AggregateException exception = Should.Throw<AggregateException>(() => manager.BeginBuild(parameters));
            exception.InnerExceptions.Count.ShouldBe(2);
            exception.InnerExceptions[0].ShouldBeOfType<InvalidOperationException>();
            exception.InnerExceptions[1].ShouldBeSameAs(shutdownFailure);
            MultiThreadedStrictModeScope.ActiveScope.ShouldBe(first);

            first.Exit();
            parameters.Loggers = [new MockLogger(_output)];
            manager.BeginBuild(parameters);
            manager.EndBuild();
        }

        [Fact]
        public void FailedStrictEntryDrainsPendingCallbacksBeforeRetry()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var first = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                Loggers = [new MockLogger(_output)],
            };
            using ManualResetEventSlim callbackStarted = new();
            using ManualResetEventSlim releaseCallback = new();
            using ManualResetEventSlim returned = new();
            LoggerException callbackFailure = new("pending callback failure");
            Exception? entryFailure = null;
            Thread thread = new(() =>
            {
                entryFailure = Record.Exception(() => manager.BeginBuild(parameters));
                returned.Set();
            });
            object stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            FieldInfo queueField = typeof(BuildManager).GetField("_workQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo buildState = typeof(BuildManager).GetField("_buildManagerState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ActionBlock<Action>? queue = null;
            bool draining = false;

            try
            {
                lock (stateLock)
                {
                    thread.Start();
                    SpinWait.SpinUntil(() => queueField.GetValue(manager) is not null, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    queue = (ActionBlock<Action>)queueField.GetValue(manager)!;
                    queue.Post(() =>
                    {
                        callbackStarted.Set();
                        releaseCallback.Wait();
                        // ProcessWorkQueue forwards failures to the same handler as logging callbacks.
                        throw callbackFailure;
                    }).ShouldBeTrue();
                    callbackStarted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                }

                SpinWait.SpinUntil(
                    () => returned.IsSet || buildState.GetValue(manager)!.ToString() == "WaitingForBuildToComplete",
                    TimeSpan.FromSeconds(10)).ShouldBeTrue();
                draining = !returned.IsSet;
            }
            finally
            {
                releaseCallback.Set();
                thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            }

            draining.ShouldBeTrue();
            queue!.Completion.IsCompleted.ShouldBeTrue();
            AggregateException exception = entryFailure.ShouldBeOfType<AggregateException>();
            exception.InnerExceptions[0].ShouldBeOfType<InvalidOperationException>();
            exception.InnerExceptions[1].ShouldBeSameAs(callbackFailure);

            first.Exit();
            parameters.Loggers = [new MockLogger(_output)];
            manager.BeginBuild(parameters);
            manager.EndBuild();
        }

        [Fact]
        public void RejectedStrictBuildDoesNotRestoreAnEndedOwnersSentinel()
        {
            _env.SetCurrentDirectory(_env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            using BuildManager owner = new();
            using BuildManager rejected = new();
            owner.BeginBuild(new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] });
            using ManualResetEventSlim callbackStarted = new();
            using ManualResetEventSlim releaseCallback = new();
            Exception? entryFailure = null;
            Thread thread = new(() => entryFailure = Record.Exception(() => rejected.BeginBuild(
                new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] })));
            object stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            FieldInfo queueField = typeof(BuildManager).GetField("_workQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo buildState = typeof(BuildManager).GetField("_buildManagerState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool ownerEnded = false;

            try
            {
                lock (stateLock)
                {
                    thread.Start();
                    SpinWait.SpinUntil(() => queueField.GetValue(rejected) is not null, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    var queue = (ActionBlock<Action>)queueField.GetValue(rejected)!;
                    queue.Post(() =>
                    {
                        callbackStarted.Set();
                        releaseCallback.Wait();
                    }).ShouldBeTrue();
                    callbackStarted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                }

                SpinWait.SpinUntil(
                    () => buildState.GetValue(rejected)!.ToString() == "WaitingForBuildToComplete",
                    TimeSpan.FromSeconds(10)).ShouldBeTrue();
                owner.EndBuild();
                ownerEnded = true;
                Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            }
            finally
            {
                releaseCallback.Set();
                thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                if (!ownerEnded)
                {
                    owner.EndBuild();
                }
            }

            entryFailure.ShouldBeOfType<InvalidOperationException>();
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void StrictRestorationFailureStillFinishesShutdown()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetCurrentDirectory(env.CreateFolder().Path);
            string originalDirectory = Directory.GetCurrentDirectory();
            MockLogger logger = new(_output);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                Loggers = [logger],
            };
            manager.BeginBuild(parameters);
            string sentinel = Directory.GetCurrentDirectory();
            Directory.Delete(originalDirectory);

            Should.Throw<DirectoryNotFoundException>(() => manager.EndBuild());
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(sentinel);
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeFalse();

            parameters.Loggers = [new MockLogger(_output)];
            manager.BeginBuild(parameters);
            manager.EndBuild();
        }

        private static BuildResult BuildStrictProject(
            string projectFile,
            MockLogger logger,
            Action<BuildManager>? configure = null,
            HostServices? hostServices = null)
        {
            using BuildManager manager = new();
            configure?.Invoke(manager);
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [logger],
                HostServices = hostServices,
            };

            return manager.Build(parameters, new BuildRequestData(projectFile, new Dictionary<string, string?>(), null, ["Build"], hostServices));
        }

        private sealed class CleanupCallbackFactory(ITaskFactory inner, Action cleanup) : ITaskFactory
        {
            public string FactoryName => inner.FactoryName;
            public Type TaskType => inner.TaskType;
            public TaskPropertyInfo[] GetTaskParameters() => inner.GetTaskParameters();
            public bool Initialize(string name, IDictionary<string, TaskPropertyInfo> parameters, string body, IBuildEngine engine)
                => inner.Initialize(name, parameters, body, engine);
            public ITask CreateTask(IBuildEngine engine) => inner.CreateTask(engine);
            public void CleanupTask(ITask task)
            {
                inner.CleanupTask(task);
                cleanup();
            }
        }

        private sealed class ScopeLifetime(MultiThreadedStrictModeScope? scope) : IDisposable
        {
            public void Dispose() => scope?.Exit();
        }
    }

    [MSBuildMultiThreadableTask]
    public class StrictLifetimeTask : Microsoft.Build.Utilities.Task
    {
        public string Violation { get; set; } = string.Empty;
        public string OtherDirectory { get; set; } = string.Empty;
        public bool ReturnFalse { get; set; }
        public bool ThrowFromGetter { get; set; }
        public bool ChangeDirectoryOnExecute { get; set; }

        [Output]
        public string Value
        {
            get
            {
                // Tasks can load in a separate assembly context; use the supplied host, not test statics.
                HostObject?.GetType().GetMethod(nameof(OutputCallbackHost.OnOutput))!.Invoke(HostObject, null);
                if (ThrowFromGetter)
                {
                    throw new InvalidOperationException("output-getter-failure");
                }

                if (Violation == "Write")
                {
                    File.WriteAllText("late-output.txt", "value");
                }
                else if (Violation == "ChangeDirectory")
                {
                    Directory.SetCurrentDirectory(OtherDirectory);
                }

                return "value";
            }
        }

        public override bool Execute()
        {
            if (ChangeDirectoryOnExecute)
            {
                Directory.SetCurrentDirectory(OtherDirectory);
            }

            return !ReturnFalse;
        }
    }

    [RunInSTA]
    [MSBuildMultiThreadableTask]
    public sealed class StrictStaLifetimeTask : StrictLifetimeTask
    {
    }

    internal sealed class OutputCallbackHost(Action callback) : ITaskHost
    {
        private Action? _callback = callback;
        public void OnOutput() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }
}
