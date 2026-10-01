// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Eventing;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
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
            SetStrictMode(_env, enabled: true);
        }

        public void Dispose() => _env.Dispose();

        public static bool FileSystemIsCaseSensitive => FileUtilities.IsFileSystemCaseSensitive;

        [Theory]
        [InlineData(true, null, true)]
        [InlineData(true, "18.12", false)]
        [InlineData(true, "18.11", false)]
        [InlineData(true, "999.999", true)]
        [InlineData(false, null, false)]
        [InlineData(false, "18.12", false)]
        public void StrictChecksDependOnMtAndChangeWave(bool multiThreaded, string? disabledWave, bool expectedStrict)
        {
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);
            ChangeWaves.ResetStateForTests();
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
            BuildParameters parameters = new();
            bool[] modes = [true, false, true];
            foreach (bool multiThreaded in modes)
            {
                parameters.MultiThreaded = multiThreaded;
                parameters.Loggers = [new MockLogger(_output)];
                manager.BeginBuild(parameters);
                try
                {
                    (MultiThreadedStrictModeScope.ActiveScope is not null).ShouldBe(multiThreaded);
                }
                finally
                {
                    manager.EndBuild();
                }

                Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ChangeWaveIsRetainedForTheProcess(bool enabled)
        {
            SetStrictMode(_env, enabled);
            using BuildManager manager = new();
            for (int i = 0; i < 2; i++)
            {
                manager.BeginBuild(new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] });
                try
                {
                    (MultiThreadedStrictModeScope.ActiveScope is not null).ShouldBe(enabled);
                }
                finally
                {
                    manager.EndBuild();
                }

                _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION",
                    enabled ? ChangeWaves.Wave18_12.ToString() : null);
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
            _env.SetCurrentDirectory(hostDirectory.Path);
            // Match the CWD's resolved spelling when the temporary directory is a symlink.
            string childPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(child.Path));
            var root = _env.CreateFile(projectDirectory, "root.proj", $"""
                <Project>
                  <ItemGroup><ProjectReference Include="child.proj" /></ItemGroup>
                  <Target Name="Build"><MSBuild Projects="{childPath}" Targets="Build" /></Target>
                </Project>
                """);
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
                manager.BuildRequest(new BuildRequestData(childPath, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
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
        public void ChangingWaveEnvironmentDuringBuildDoesNotChangeTaskChecks(bool optOut)
        {
            SetStrictMode(_env, enabled: !optOut);
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
                _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION",
                    optOut ? null : ChangeWaves.Wave18_12.ToString());
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

        [Fact]
        public void ScopeNeverAdoptsADirectoryChangedBeforePublication()
        {
            var foreignDirectory = _env.CreateFolder();
            var protectedFile = _env.CreateFile(foreignDirectory, "keep.txt", "must not be deleted");
            string? installedDirectory = null;
            string? changedDirectory = null;
            var scope = MultiThreadedStrictModeScope.Enter(0, MultiThreadedStrictModeScope.CaptureCurrentDirectory(), path =>
            {
                installedDirectory = path;
                Directory.SetCurrentDirectory(path);
                Directory.SetCurrentDirectory(foreignDirectory.Path);
                changedDirectory = Directory.GetCurrentDirectory();
            });
            using var lifetime = new ScopeLifetime(scope);

            scope.SentinelDirectory.ShouldBe(installedDirectory);
            scope.DetectCurrentDirectoryViolation().ShouldBe(changedDirectory);
            scope.DetectUnresolvedPathWrites().ShouldBeNull();
            File.ReadAllText(protectedFile.Path).ShouldBe("must not be deleted");
            Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
        }

        [Fact]
        public void ScopeCleansItsRootWhenChangingDirectoryFails()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            string? temporaryDirectory = null;
            IOException failure = new("injected CWD switch failure");

            Should.Throw<IOException>(() => MultiThreadedStrictModeScope.Enter(
                0, MultiThreadedStrictModeScope.CaptureCurrentDirectory(), path =>
                {
                    temporaryDirectory = Path.GetDirectoryName(path);
                    throw failure;
                })).ShouldBeSameAs(failure);

            temporaryDirectory.ShouldNotBeNull();
            Directory.Exists(temporaryDirectory).ShouldBeFalse();
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void FailedEntryCleansItsRootEvenWhenRestorationFails()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var directory = _env.CreateFolder();
            _env.SetCurrentDirectory(directory.Path);
            var snapshot = MultiThreadedStrictModeScope.CaptureCurrentDirectory();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(directory.Path);
            string? temporaryDirectory = null;
            IOException entryFailure = new("injected CWD switch failure");

            var exception = Should.Throw<AggregateException>(() => MultiThreadedStrictModeScope.Enter(0, snapshot, path =>
            {
                temporaryDirectory = Path.GetDirectoryName(path);
                throw entryFailure;
            }));

            exception.InnerExceptions.Count.ShouldBe(2);
            exception.InnerExceptions[0].ShouldBeSameAs(entryFailure);
            exception.InnerExceptions[1].ShouldBeOfType<DirectoryNotFoundException>();
            temporaryDirectory.ShouldNotBeNull();
            Directory.Exists(temporaryDirectory).ShouldBeFalse();
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
        }

        [Fact]
        public void InactiveScopeDoesNotInspectAnEndedSentinel()
        {
            var first = MultiThreadedStrictModeScope.Enter(0);
            using var firstLifetime = new ScopeLifetime(first);
            first.Exit();
            first.DetectUnresolvedPathWrites().ShouldBeNull();

            var second = MultiThreadedStrictModeScope.Enter(1);
            using var secondLifetime = new ScopeLifetime(second);
            File.WriteAllText("second.txt", "content");

            first.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out bool recovered);

            recovered.ShouldBeFalse();
            File.Exists(Path.Combine(second.SentinelDirectory, "second.txt")).ShouldBeTrue();
            second.DetectUnresolvedPathWrites().ShouldBe("second.txt");
        }

        [Fact]
        public void UnchangedCurrentDirectoryCheckDoesNotAcquireStateLock()
        {
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            System.Threading.Tasks.Task<string?> check;
            bool completed;
            LockType stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null).ShouldBeOfType<LockType>();
            lock (stateLock)
            {
                check = System.Threading.Tasks.Task.Run(scope.DetectCurrentDirectoryViolation);
                completed = check.Wait(TimeSpan.FromSeconds(10));
            }

            check.GetAwaiter().GetResult().ShouldBeNull();
            completed.ShouldBeTrue();
        }

        [Theory]
        [InlineData("Empty")]
        [InlineData("Write")]
        [InlineData("Missing")]
        [InlineData("Blocked")]
        public void DirectoryScansEmitPairedPerformanceEvents(string state)
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var scope = MultiThreadedStrictModeScope.Enter(42);
            using var lifetime = new ScopeLifetime(scope);
            if (state == "Write")
            {
                File.WriteAllText("scan-event.txt", "content");
            }
            else if (state is "Missing" or "Blocked")
            {
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(scope.SentinelDirectory);
                if (state == "Blocked")
                {
                    File.WriteAllText(scope.SentinelDirectory, "occupied");
                }
            }

            using EventSourceTestHelper listener = new();
            string? entries = null;
            bool recovered = false;
            Exception? exception = Record.Exception(() => entries = scope.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out recovered));
            if (state == "Write")
            {
                exception.ShouldBeNull();
                entries.ShouldBe("scan-event.txt");
            }
            else if (state == "Missing")
            {
                exception.ShouldBeNull();
                recovered.ShouldBeTrue();
                Directory.Exists(scope.SentinelDirectory).ShouldBeTrue();
                Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
            }
            else if (state == "Blocked")
            {
                exception.ShouldBeOfType<InvalidProjectFileException>().ErrorCode.ShouldBe("MSB4290");
                recovered.ShouldBeFalse();
            }
            else
            {
                exception.ShouldBeNull();
            }

            var events = listener.GetEvents();
            events.ShouldNotContain(e => e.EventId == 0);
            var scans = events.FindAll(e => e.EventName is
                nameof(MSBuildEventSource.StrictModeDirectoryScanStart) or
                nameof(MSBuildEventSource.StrictModeDirectoryScanStop));
            scans.Count.ShouldBe(2);
            scans[0].EventName.ShouldBe(nameof(MSBuildEventSource.StrictModeDirectoryScanStart));
            scans[1].EventName.ShouldBe(nameof(MSBuildEventSource.StrictModeDirectoryScanStop));
            foreach (var scan in scans)
            {
                var payload = scan.Payload.ShouldNotBeNull();
                payload[0].ShouldBe(42);
                payload[1].ShouldBe(string.Empty);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingSentinelRecoveryRestoresFutureChecks(bool removeRoot)
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var scope = MultiThreadedStrictModeScope.Enter(42);
            using var lifetime = new ScopeLifetime(scope);
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(scope.SentinelDirectory);
            if (removeRoot)
            {
                Directory.Delete(Path.GetDirectoryName(scope.SentinelDirectory)!);
            }

            scope.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out bool recovered).ShouldBeNull();

            recovered.ShouldBeTrue();
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeSameAs(scope);
            Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
            File.WriteAllText("after-recovery.txt", "content");
            scope.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out recovered).ShouldBe("after-recovery.txt");
            recovered.ShouldBeFalse();
        }

        [UnixOnlyFact]
        public void RecoveryReentersAnUnlinkedCurrentDirectory()
        {
            var scope = MultiThreadedStrictModeScope.Enter(42);
            using var lifetime = new ScopeLifetime(scope);
            Directory.Delete(scope.SentinelDirectory);

            scope.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out bool recovered).ShouldBeNull();

            recovered.ShouldBeTrue();
            Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
            File.WriteAllText("after-recovery.txt", "content");
            scope.DetectUnresolvedPathWrites().ShouldBe("after-recovery.txt");
        }

        [Fact]
        public void ConcurrentScansRecoverTheMissingSentinelOnce()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var scope = MultiThreadedStrictModeScope.Enter(42);
            using var lifetime = new ScopeLifetime(scope);
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(scope.SentinelDirectory);
            int recoveries = 0;

            System.Threading.Tasks.Parallel.For(0, 16, _ =>
            {
                scope.VerifyUnresolvedPathWrites(ElementLocation.EmptyLocation, out bool recovered).ShouldBeNull();
                if (recovered)
                {
                    Interlocked.Increment(ref recoveries);
                }
            });

            recoveries.ShouldBe(1);
            Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
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
                var exception = Should.Throw<InvalidOperationException>(() => MultiThreadedStrictModeScope.Enter(1));
                exception.Message.ShouldContain("MSB4289");
                exception.Message.ShouldContain("MSBUILDDISABLEFEATURESFROMVERSION=18.12");
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
        /// exactly once so that subsequent project checks do not repeat the diagnostic.
        /// </summary>
        [Fact]
        public void UnresolvedPathWriteIsDetectedOnceAndRemoved()
        {
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                scope.DetectUnresolvedPathWrites().ShouldBeNull();

                // A relative path resolves against the process current directory, which is the whole defect.
                File.WriteAllText("unresolved.txt", "probe");

                scope.DetectUnresolvedPathWrites().ShouldBe("unresolved.txt");
                scope.DetectCurrentDirectoryViolation().ShouldBeNull();

                // Removed, so that it cannot satisfy a later task's unresolved read, and not reported again.
                File.Exists(Path.Combine(scope.SentinelDirectory, "unresolved.txt")).ShouldBeFalse();
                scope.DetectUnresolvedPathWrites().ShouldBeNull();
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

                    scope.DetectUnresolvedPathWrites().ShouldBe(name, $"iteration {i}");
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
                scope.DetectUnresolvedPathWrites().ShouldBe(Name);
                File.Exists(Name).ShouldBeTrue();
            }

            File.Delete(Name);
            scope.DetectUnresolvedPathWrites().ShouldBeNull();
            File.WriteAllText(Name, "new output");

            scope.DetectUnresolvedPathWrites().ShouldBe(Name);
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
                scope.DetectUnresolvedPathWrites().ShouldBe(Name);
                scope.DetectUnresolvedPathWrites().ShouldBeNull();
            }

            scope.DetectUnresolvedPathWrites().ShouldBeNull();
            File.Exists(Name).ShouldBeFalse();
        }

        [Theory]
        [InlineData(25, 0)]
        [InlineData(40, 40)]
        [InlineData(512, 64)]
        public void UnresolvedPathWritesAreReportedAndRemovedInOneCheck(int files, int directories)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            string[] expected = new string[files + directories];

            for (int i = 0; i < files; i++)
            {
                expected[i] = $"stray{i:D2}.txt";
                File.WriteAllText(expected[i], "probe");
            }

            for (int i = 0; i < directories; i++)
            {
                string directory = expected[files + i] = $"directory{i:D2}";
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "nested.txt"), "probe");
            }

            string? reported = scope.DetectUnresolvedPathWrites();
            reported.ShouldNotBeNull();
            reported!.Split([", "], StringSplitOptions.None).ShouldBe(expected, ignoreOrder: true);
            Directory.EnumerateFileSystemEntries(scope.SentinelDirectory).ShouldBeEmpty();
            scope.DetectUnresolvedPathWrites().ShouldBeNull();
        }

        [Fact]
        public void CurrentDirectoryChangeIsDetectedOnceAndRepaired()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter(0);

            try
            {
                Directory.SetCurrentDirectory(originalDirectory);

                scope.DetectCurrentDirectoryViolation().ShouldNotBeNull();

                // Repaired, so the rest of the build keeps the protection it asked for.
                Path.GetFileName(Directory.GetCurrentDirectory())
                    .ShouldBe(MultiThreadedStrictModeScope.SentinelDirectoryName);

                scope.DetectCurrentDirectoryViolation().ShouldBeNull();
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
                scope.DetectCurrentDirectoryViolation().ShouldBe(originalDirectory);
                Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
                scope.DetectCurrentDirectoryViolation().ShouldBeNull();
            }
        }

        [Fact(Skip = "Requires a case-sensitive file system.", SkipUnless = nameof(FileSystemIsCaseSensitive))]
        public void CaseDistinctSiblingDirectoryIsDetectedAndRepaired()
        {
            var scope = MultiThreadedStrictModeScope.Enter(0);
            using var lifetime = new ScopeLifetime(scope);
            string sibling = Path.Combine(Path.GetDirectoryName(scope.SentinelDirectory)!, "mt-sentinel-cwd");
            Directory.CreateDirectory(sibling);
            Directory.SetCurrentDirectory(sibling);

            scope.DetectCurrentDirectoryViolation().ShouldBe(sibling);
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
        [InlineData("ErrorAndStop", false)]
        [InlineData("ErrorAndContinue", false)]
        [InlineData("WarnAndContinue", false)]
        [InlineData("ErrorAndStop", true)]
        [InlineData("ErrorAndContinue", true)]
        [InlineData("WarnAndContinue", true)]
        public void MissingSentinelRecoversUnlessItsPathIsBlocked(string continueOnError, bool blockRecovery)
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
                    <Message Text="AFTER-RECOVERY:$(MSBuildLastTaskResult)" Importance="high" />
                  </Target>
                </Project>
                """);
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictLifetimeTask), new OutputCallbackHost(() =>
            {
                string sentinel = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(sentinel);
                if (blockRecovery)
                {
                    File.WriteAllText(sentinel, "occupied");
                }
            }));
            MockLogger logger = new(_output);
            using EventSourceTestHelper listener = new();

            BuildResult result = BuildStrictProject(project.Path, logger, hostServices: hostServices);
            result.OverallResult.ShouldBe(blockRecovery ? BuildResultCode.Failure : BuildResultCode.Success);
            if (blockRecovery)
            {
                logger.Errors.ShouldContain(e => e.Code == "MSB4290"
                    && e.BuildEventContext!.TaskId != BuildEventContext.InvalidTaskId);
                logger.Errors.ShouldAllBe(e => e.Code == "MSB4290");
                logger.AssertNoWarnings();
                logger.AssertLogDoesntContain("AFTER-RECOVERY");
            }
            else
            {
                logger.AssertNoErrors();
                logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4288");
                logger.AssertLogContains("AFTER-RECOVERY:true");
            }
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBe(!blockRecovery);
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);

            var projectEvents = listener.GetEvents().FindAll(e => e.EventName is
                nameof(MSBuildEventSource.BuildProjectStart) or nameof(MSBuildEventSource.BuildProjectStop));
            projectEvents.Count.ShouldBe(2);
            projectEvents[0].EventName.ShouldBe(nameof(MSBuildEventSource.BuildProjectStart));
            projectEvents[1].EventName.ShouldBe(nameof(MSBuildEventSource.BuildProjectStop));
        }

        /// <summary>
        /// Strict mode is meaningless outside multi-threaded mode, so a build that is not multi-threaded must not
        /// have the process moved out from under it when strict checks have not been opted out.
        /// </summary>
        [Fact]
        public void StrictModeIsIgnoredWhenBuildIsNotMultiThreaded()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            SetStrictMode(env, enabled: true);

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
        public void MtBuildPreservesStrictAndLegacyRestoration(bool strict, bool saveEnvironment)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            SetStrictMode(env, enabled: strict);
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
            SetStrictMode(env, enabled: false);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NonStrictEmptyBuildDoesNotRestoreProcessDirectory(bool saveEnvironment)
        {
            SetStrictMode(_env, enabled: false);
            _env.SetCurrentDirectory(_env.CreateFolder().Path);
            string changedDirectory = _env.CreateFolder().Path;
            using BuildManager manager = new();
            manager.BeginBuild(new BuildParameters
            {
                MultiThreaded = true,
                SaveOperatingEnvironment = saveEnvironment,
                Loggers = [new MockLogger(_output)],
            });
            try
            {
                Directory.SetCurrentDirectory(changedDirectory);
                changedDirectory = Directory.GetCurrentDirectory();
            }
            finally
            {
                manager.EndBuild();
            }

            Directory.GetCurrentDirectory().ShouldBe(changedDirectory);
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
            second.DetectUnresolvedPathWrites().ShouldBeNull();
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
            LockType stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null).ShouldBeOfType<LockType>();
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
        [InlineData("Write", "ErrorAndStop", true)]
        [InlineData("Write", "ErrorAndContinue", true)]
        [InlineData("Write", "WarnAndContinue", true)]
        [InlineData("ChangeDirectory", "ErrorAndStop", false)]
        [InlineData("ChangeDirectory", "ErrorAndContinue", false)]
        [InlineData("ChangeDirectory", "WarnAndContinue", true)]
        public void OutputGetterViolationUsesItsBoundaryFailurePolicy(string violation, string continueOnError, bool succeeds)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            SetStrictMode(env, enabled: true);
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
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBe(violation == "Write");
            if (violation == "Write")
            {
                logger.AssertLogContains("AFTER:true:value");
            }
            else if (continueOnError == "ErrorAndStop")
            {
                logger.AssertLogDoesntContain("AFTER:");
            }
            else
            {
                logger.AssertLogContains("AFTER:false:value");
            }
        }

        [Fact]
        public void RelativeWriteIsLeftUntilProjectCompletion()
        {
            var project = _env.CreateFile("deferred-write.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write" ContinueOnError="WarnAndContinue">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                    <StrictLifetimeTask>
                      <Output TaskParameter="RelativeFileExists" PropertyName="FileExists" />
                    </StrictLifetimeTask>
                    <Message Text="BEFORE_PROJECT_END:$(FileExists):$(MSBuildLastTaskResult)" Importance="high" />
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);

            BuildStrictProject(project.Path, logger).ShouldHaveSucceeded();

            logger.AssertLogContains("BEFORE_PROJECT_END:True:true", "MSB4287");
            logger.TaskFinishedEvents.ShouldAllBe(e => e.Succeeded);
            logger.ProjectFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.Warnings.ShouldHaveSingleItem().BuildEventContext!.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
            logger.AssertNoErrors();
        }

        [Fact]
        public void SentinelScanLockDoesNotBlockTaskCompletion()
        {
            var project = _env.CreateFile("scan-boundary.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            using ManualResetEventSlim taskFinished = new();
            MockLogger logger = new(_output);
            using BuildManager manager = new();
            manager.BeginBuild(new BuildParameters
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers =
                [
                    logger,
                    new InitializationCallbackLogger(() => { }, source =>
                        source.TaskFinished += (_, _) => taskFinished.Set()),
                ],
            });
            var scope = MultiThreadedStrictModeScope.ActiveScope.ShouldNotBeNull();
            object scanLock = typeof(MultiThreadedStrictModeScope)
                .GetField("_reportedEntriesLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope)!;
            try
            {
                BuildSubmission submission = manager.PendBuildRequest(
                    new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null));
                lock (scanLock)
                {
                    submission.ExecuteAsync(null, null);
                    taskFinished.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    submission.WaitHandle.WaitOne(0).ShouldBeFalse();
                    File.Exists(Path.Combine(scope.SentinelDirectory, "late-output.txt")).ShouldBeTrue();
                }

                submission.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                submission.BuildResult!.ShouldHaveSucceeded();
            }
            finally
            {
                manager.EndBuild();
            }

            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4287");
            logger.AssertNoErrors();
        }

        [Fact]
        public void WriteRemovedBeforeProjectCompletionIsNotReported()
        {
            var project = _env.CreateFile("transient-write.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                    <StrictLifetimeTask Violation="Delete">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);

            BuildStrictProject(project.Path, logger).ShouldHaveSucceeded();

            logger.AssertNoErrors();
            logger.TaskFinishedEvents.Count.ShouldBe(2);
            logger.TaskFinishedEvents.ShouldAllBe(e => e.Succeeded);
        }

        [Theory]
        [InlineData("Default", "", false, false)]
        [InlineData("PromoteCode", "", true, false)]
        [InlineData("PromoteAll", "", true, false)]
        [InlineData("Suppress", "", false, true)]
        [InlineData("SuppressPromoted", "", false, true)]
        [InlineData("ExcludeFromAll", "", false, false)]
        [InlineData("ExcludeSpecific", "", true, false)]
        [InlineData("PromoteOther", "", false, false)]
        [InlineData("Default", "<MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>", true, false)]
        [InlineData("Default", "<MSBuildWarningsAsErrors>MSB4287</MSBuildWarningsAsErrors>", true, false)]
        [InlineData("Default", "<MSBuildWarningsAsMessages>MSB4287</MSBuildWarningsAsMessages>", false, true)]
        [InlineData("PromoteCode", "<MSBuildWarningsAsMessages>MSB4287</MSBuildWarningsAsMessages>", false, true)]
        [InlineData("Suppress", "<MSBuildWarningsAsErrors>MSB4287</MSBuildWarningsAsErrors>", false, true)]
        [InlineData("PromoteAll", "<MSBuildWarningsNotAsErrors>MSB4287</MSBuildWarningsNotAsErrors>", false, false)]
        [InlineData("ExcludeFromAll", "<MSBuildWarningsAsErrors>MSB4287</MSBuildWarningsAsErrors>", true, false)]
        public void ProjectWriteUsesNormalWarningPolicy(string policy, string projectPolicy, bool promoted, bool suppressed)
        {
            var project = _env.CreateFile("warning-policy.proj", $"""
                <Project>
                  <PropertyGroup>{projectPolicy}</PropertyGroup>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            BuildParameters parameters = CreateStrictWarningParameters(logger, policy);
            using BuildManager manager = new();

            BuildResult result = manager.Build(parameters,
                new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null));

            result.OverallResult.ShouldBe(promoted ? BuildResultCode.Failure : BuildResultCode.Success);
            result.Exception.ShouldBeNull();
            BuildEventArgs diagnostic = AssertStrictDiagnostic(logger, promoted, suppressed);
            diagnostic.BuildEventContext!.ProjectContextId.ShouldBe(
                logger.ProjectStartedEvents.ShouldHaveSingleItem().BuildEventContext!.ProjectContextId);
            diagnostic.BuildEventContext.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBe(!promoted);
        }

        [Fact]
        public void ProjectWriteWarningDoesNotFailCachedResults()
        {
            var project = _env.CreateFile("cached-write.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            string outputCache = Path.Combine(_env.CreateFolder().Path, "results.cache");
            using BuildManager manager = new();
            manager.BeginBuild(new BuildParameters
            {
                MultiThreaded = true,
                OutputResultsCacheFile = outputCache,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [logger],
            });
            try
            {
                BuildRequestData request = new(project.Path, new Dictionary<string, string?>(), null, ["Build"], null);
                for (int i = 0; i < 2; i++)
                {
                    BuildResult result = manager.BuildRequest(request);
                    result.ShouldHaveSucceeded();
                    result.Exception.ShouldBeNull();
                }
            }
            finally
            {
                manager.EndBuild();
            }

            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4287");
            logger.AssertNoErrors();
            File.Exists(outputCache).ShouldBeTrue();

            MockLogger cachedLogger = new(_output);
            using BuildManager cacheReader = new();
            cacheReader.Build(new BuildParameters
            {
                MultiThreaded = true,
                InputResultsCacheFiles = [outputCache],
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                Loggers = [cachedLogger],
            }, new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null)).ShouldHaveSucceeded();
            cachedLogger.TaskFinishedEvents.ShouldBeEmpty();
            cachedLogger.AssertNoErrors();
            cachedLogger.AssertNoWarnings();
        }

        [Theory]
        [InlineData("PromoteCode", "")]
        [InlineData("PromoteAll", "")]
        [InlineData("Default", "<MSBuildWarningsAsErrors>MSB4287</MSBuildWarningsAsErrors>")]
        [InlineData("Default", "<MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>")]
        public void ProjectWritePromotionUsesStandardCachedWarningSemantics(string policy, string projectPolicy)
        {
            var project = _env.CreateFile("cached-promoted-write.proj", $"""
                <Project>
                  <PropertyGroup>{projectPolicy}</PropertyGroup>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            using BuildManager manager = new();
            manager.BeginBuild(CreateStrictWarningParameters(logger, policy));
            try
            {
                BuildRequestData request = new(project.Path, new Dictionary<string, string?>(), null, ["Build"], null);
                for (int i = 0; i < 2; i++)
                {
                    BuildResult result = manager.BuildRequest(request);
                    result.OverallResult.ShouldBe(i == 0 ? BuildResultCode.Failure : BuildResultCode.Success);
                    result.Exception.ShouldBeNull();
                }
            }
            finally
            {
                manager.EndBuild();
            }

            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            AssertStrictDiagnostic(logger, promoted: true, suppressed: false);
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
        }

        [Fact]
        public void ProjectWriteWarningDoesNotAdoptAnotherPromotedWarning()
        {
            var project = _env.CreateFile("unrelated-promoted-warning.proj", $"""
                <Project>
                  <UsingTask TaskName="StrictLifetimeTask" AssemblyFile="{typeof(StrictLifetimeTask).Assembly.Location}" />
                  <Target Name="Build">
                    <Warning Code="OTHER0001" Text="Unrelated warning" />
                    <StrictLifetimeTask Violation="Write">
                      <Output TaskParameter="Value" PropertyName="Value" />
                    </StrictLifetimeTask>
                  </Target>
                </Project>
                """);
            MockLogger logger = new(_output);
            using BuildManager manager = new();

            BuildResult result = manager.Build(CreateStrictWarningParameters(logger, "PromoteOther"),
                new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null));

            result.ShouldHaveFailed();
            result.Exception.ShouldBeNull();
            logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("OTHER0001");
            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4287");
        }

        [Theory]
        [InlineData("Default", false, false, false)]
        [InlineData("PromoteCode", true, false, false)]
        [InlineData("PromoteAll", true, false, false)]
        [InlineData("Suppress", false, true, false)]
        [InlineData("SuppressPromoted", false, true, false)]
        [InlineData("ExcludeFromAll", false, false, false)]
        [InlineData("ExcludeSpecific", true, false, false)]
        [InlineData("PromoteOther", false, false, false)]
        [InlineData("Default", false, false, true)]
        [InlineData("PromoteCode", true, false, true)]
        [InlineData("PromoteAll", true, false, true)]
        [InlineData("Suppress", false, true, true)]
        [InlineData("SuppressPromoted", false, true, true)]
        [InlineData("ExcludeFromAll", false, false, true)]
        [InlineData("ExcludeSpecific", true, false, true)]
        [InlineData("PromoteOther", false, false, true)]
        public void FinalBuildSweepUsesGlobalWarningPolicyBeforeSerializingCaches(string policy, bool promoted, bool suppressed, bool missingSentinel)
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            string code = missingSentinel ? "MSB4288" : "MSB4287";
            var project = _env.CreateFile("late-write.proj", $"""
                <Project>
                  <PropertyGroup>
                    <MSBuildWarningsAsErrors>{code}</MSBuildWarningsAsErrors>
                    <MSBuildWarningsAsMessages>{code}</MSBuildWarningsAsMessages>
                    <MSBuildWarningsNotAsErrors>{code}</MSBuildWarningsNotAsErrors>
                  </PropertyGroup>
                  <Target Name="Build" />
                </Project>
                """);
            string outputCache = Path.Combine(_env.CreateFolder().Path, "results.cache");
            MockLogger logger = new(_output);
            using BuildManager manager = new();
            BuildParameters parameters = CreateStrictWarningParameters(logger, policy, code);
            parameters.OutputResultsCacheFile = outputCache;
            manager.BeginBuild(parameters);
            string sentinel = MultiThreadedStrictModeScope.ActiveScope.ShouldNotBeNull().SentinelDirectory;
            manager.BuildRequest(new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null))
                .ShouldHaveSucceeded();
            if (missingSentinel)
            {
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(sentinel);
            }
            else
            {
                File.WriteAllText(Path.Combine(sentinel, "late.txt"), "late output");
            }

            Exception? exception = Record.Exception(manager.EndBuild);
            if (promoted)
            {
                var failure = exception.ShouldBeOfType<InvalidProjectFileException>();
                failure.ErrorCode.ShouldBe(code);
                failure.HasBeenLogged.ShouldBeTrue();
                failure.ProjectFile.ShouldBeEmpty();
            }
            else
            {
                exception.ShouldBeNull();
            }

            BuildEventArgs diagnostic = AssertStrictDiagnostic(logger, promoted, suppressed, code);
            diagnostic.BuildEventContext.ShouldBe(BuildEventContext.Invalid);
            string? file = diagnostic switch
            {
                BuildWarningEventArgs warning => warning.File,
                BuildErrorEventArgs error => error.File,
                BuildMessageEventArgs message => message.File,
                _ => throw new InvalidOperationException(),
            };
            file.ShouldBeEmpty();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBe(!promoted);
            File.Exists(outputCache).ShouldBe(!promoted);
            Directory.Exists(sentinel).ShouldBeFalse();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();

            manager.BeginBuild(new BuildParameters { MultiThreaded = true, Loggers = [new MockLogger(_output)] });
            manager.EndBuild();
        }

        [Theory]
        [InlineData("Default", false, false)]
        [InlineData("PromoteCode", true, false)]
        [InlineData("SuppressPromoted", false, true)]
        public void ProjectScanRecoversMissingSentinel(string policy, bool promoted, bool suppressed)
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var project = _env.CreateFile("project-recovery.proj", """
                <Project><Target Name="Build"><Message Text="built" /></Target></Project>
                """);
            using ManualResetEventSlim taskFinished = new();
            MockLogger logger = new(_output);
            BuildParameters parameters = CreateStrictWarningParameters(logger, policy, "MSB4288");
            parameters.Loggers =
            [
                logger,
                new InitializationCallbackLogger(() => { }, source => source.TaskFinished += (_, _) => taskFinished.Set()),
            ];
            using BuildManager manager = new();
            manager.BeginBuild(parameters);
            try
            {
                var scope = MultiThreadedStrictModeScope.ActiveScope.ShouldNotBeNull();
                object scanLock = typeof(MultiThreadedStrictModeScope)
                    .GetField("_reportedEntriesLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope)!;
                BuildSubmission submission = manager.PendBuildRequest(
                    new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null));
                lock (scanLock)
                {
                    submission.ExecuteAsync(null, null);
                    taskFinished.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    Directory.SetCurrentDirectory(originalDirectory);
                    Directory.Delete(scope.SentinelDirectory);
                }

                submission.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                submission.BuildResult!.OverallResult.ShouldBe(promoted ? BuildResultCode.Failure : BuildResultCode.Success);
                Directory.GetCurrentDirectory().ShouldBe(scope.SentinelDirectory);
            }
            finally
            {
                manager.EndBuild();
            }

            BuildEventArgs diagnostic = AssertStrictDiagnostic(logger, promoted, suppressed, "MSB4288");
            BuildEventContext context = diagnostic.BuildEventContext.ShouldNotBeNull();
            context.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
            context.ProjectContextId.ShouldNotBe(BuildEventContext.InvalidProjectContextId);
            logger.TaskFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBe(!promoted);
        }

        [Fact]
        public void FinalRecoveryFailureCannotBeSuppressed()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            var project = _env.CreateFile("failed-recovery.proj", """<Project><Target Name="Build" /></Project>""");
            string outputCache = Path.Combine(_env.CreateFolder().Path, "results.cache");
            MockLogger logger = new(_output);
            BuildParameters parameters = CreateStrictWarningParameters(logger, "Default");
            parameters.OutputResultsCacheFile = outputCache;
            parameters.WarningsAsMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MSB4288", "MSB4290" };
            using BuildManager manager = new();
            manager.BeginBuild(parameters);
            string sentinel = MultiThreadedStrictModeScope.ActiveScope.ShouldNotBeNull().SentinelDirectory;
            manager.BuildRequest(new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null))
                .ShouldHaveSucceeded();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(sentinel);
            File.WriteAllText(sentinel, "occupied");

            var failure = Should.Throw<InvalidProjectFileException>(manager.EndBuild);

            failure.ErrorCode.ShouldBe("MSB4290");
            logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("MSB4290");
            logger.AssertNoWarnings();
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
            File.Exists(outputCache).ShouldBeFalse();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
        }

        [Theory]
        [InlineData("Default", false, false)]
        [InlineData("PromoteCode", true, false)]
        [InlineData("SuppressPromoted", false, true)]
        public void FinalBuildSweepDoesNotReturnSuccessForPromotedWarning(string policy, bool promoted, bool suppressed)
        {
            var project = _env.CreateFile("late-build-result.proj", """<Project><Target Name="Build" /></Project>""");
            string outputCache = Path.Combine(_env.CreateFolder().Path, "results.cache");
            MockLogger logger = new(_output);
            BuildParameters parameters = CreateStrictWarningParameters(logger, policy);
            parameters.OutputResultsCacheFile = outputCache;
            parameters.Loggers =
            [
                logger,
                new InitializationCallbackLogger(() => { }, source =>
                    source.ProjectFinished += (_, _) => File.WriteAllText(
                        Path.Combine(MultiThreadedStrictModeScope.ActiveScope!.SentinelDirectory, "late.txt"), "late output")),
            ];
            using BuildManager manager = new();
            BuildResult? result = null;

            Exception? exception = Record.Exception(() => result = manager.Build(parameters,
                new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null)));

            if (promoted)
            {
                var failure = exception.ShouldBeOfType<InvalidProjectFileException>();
                failure.ErrorCode.ShouldBe("MSB4287");
                failure.HasBeenLogged.ShouldBeTrue();
                result.ShouldBeNull();
            }
            else
            {
                exception.ShouldBeNull();
                result.ShouldNotBeNull().ShouldHaveSucceeded();
            }

            AssertStrictDiagnostic(logger, promoted, suppressed).BuildEventContext.ShouldBe(BuildEventContext.Invalid);
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBe(!promoted);
            File.Exists(outputCache).ShouldBe(!promoted);
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
        }

        [Fact]
        public void FinalBuildSweepDoesNotAttributeEarlierErrorsToItsWarning()
        {
            var project = _env.CreateFile("earlier-error.proj", """<Project><Target Name="Build" /></Project>""");
            string outputCache = Path.Combine(_env.CreateFolder().Path, "results.cache");
            MockLogger logger = new(_output);
            BuildParameters parameters = CreateStrictWarningParameters(logger, "PromoteOther");
            parameters.OutputResultsCacheFile = outputCache;
            using BuildManager manager = new();
            manager.BeginBuild(parameters);
            try
            {
                manager.BuildRequest(new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null))
                    .ShouldHaveSucceeded();
                ((IBuildComponentHost)manager).LoggingService.LogWarningFromText(
                    BuildEventContext.Invalid, null, "OTHER0001", null, BuildEventFileInfo.Empty, "Unrelated warning");
                File.WriteAllText(Path.Combine(MultiThreadedStrictModeScope.ActiveScope!.SentinelDirectory, "late.txt"), "late output");
            }
            finally
            {
                manager.EndBuild();
            }

            logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("OTHER0001");
            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4287");
            logger.BuildFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
            File.Exists(outputCache).ShouldBeTrue();
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
            BuildResult result = BuildStrictProject(project.Path, logger);
            result.OverallResult.ShouldBe(violation == "Write" && !returnFalse ? BuildResultCode.Success : BuildResultCode.Failure);
            if (violation == "Write")
            {
                logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB4287");
                logger.Errors.Count.ShouldBe(returnFalse ? 1 : 0);
            }
            else
            {
                logger.AssertNoWarnings();
                logger.Errors.Count.ShouldBe(returnFalse ? 2 : 1);
                logger.Errors[logger.Errors.Count - 1].Code.ShouldBe("MSB4286");
            }

            if (returnFalse)
            {
                logger.Errors[0].Code.ShouldBe("MSB4181");
            }
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded
                .ShouldBe(violation == "Write" && !returnFalse);
        }

        [WindowsFullFrameworkOnlyFact]
        public void StaOutputWriteIsDetectedAtProjectCompletion()
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
            logger.AssertLogContains("STA_RESULT:true:value", "MSB4287");
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictStaLifetimeTask))!.Succeeded.ShouldBeTrue();
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
        [InlineData("Write", "ErrorAndStop", true)]
        [InlineData("Write", "ErrorAndContinue", true)]
        [InlineData("Write", "WarnAndContinue", true)]
        [InlineData("ChangeDirectory", "ErrorAndStop", false)]
        [InlineData("ChangeDirectory", "ErrorAndContinue", false)]
        [InlineData("ChangeDirectory", "WarnAndContinue", true)]
        [InlineData("Throw", "ErrorAndStop", false)]
        [InlineData("Throw", "ErrorAndContinue", false)]
        [InlineData("Throw", "WarnAndContinue", false)]
        public void CleanupViolationUsesItsBoundaryFailurePolicy(string violation, string continueOnError, bool succeeds)
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
            logger.TaskFinishedEvents[0].Succeeded.ShouldBe(violation == "Write");
            logger.ErrorCount.ShouldBe(succeeds ? 0 : 1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancellationDuringOutputGatheringPreservesEarlierDiagnostics(bool strict)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            SetStrictMode(env, enabled: strict);
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
                if (MultiThreadedStrictModeScope.ActiveScope is { } scope)
                {
                    File.WriteAllText(Path.Combine(scope.SentinelDirectory, "canceled-write.txt"), "canceled");
                }
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

            Should.Throw<DirectoryNotFoundException>(() => scope.DetectCurrentDirectoryViolation());
            Should.Throw<DirectoryNotFoundException>(() => scope.DetectUnresolvedPathWrites());
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
            using ManualResetEventSlim loggerInitializing = new();
            using ManualResetEventSlim releaseLogger = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                Loggers =
                [
                    new MockLogger(_output),
                    new InitializationCallbackLogger(() =>
                    {
                        loggerInitializing.Set();
                        releaseLogger.Wait();
                    }),
                ],
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
            LockType stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null).ShouldBeOfType<LockType>();
            FieldInfo queueField = typeof(BuildManager).GetField("_workQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo buildState = typeof(BuildManager).GetField("_buildManagerState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ActionBlock<Action>? queue = null;
            bool draining = false;

            try
            {
                thread.Start();
                loggerInitializing.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                lock (stateLock)
                {
                    releaseLogger.Set();
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
                releaseLogger.Set();
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
            using ManualResetEventSlim loggerInitializing = new();
            using ManualResetEventSlim releaseLogger = new();
            Exception? entryFailure = null;
            Thread thread = new(() => entryFailure = Record.Exception(() => rejected.BeginBuild(
                new BuildParameters
                {
                    MultiThreaded = true,
                    Loggers =
                    [
                        new MockLogger(_output),
                        new InitializationCallbackLogger(() =>
                        {
                            loggerInitializing.Set();
                            releaseLogger.Wait();
                        }),
                    ],
                })));
            LockType stateLock = typeof(MultiThreadedStrictModeScope)
                .GetField("s_stateLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null).ShouldBeOfType<LockType>();
            FieldInfo queueField = typeof(BuildManager).GetField("_workQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo buildState = typeof(BuildManager).GetField("_buildManagerState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool ownerEnded = false;

            try
            {
                thread.Start();
                loggerInitializing.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                lock (stateLock)
                {
                    releaseLogger.Set();
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
                releaseLogger.Set();
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

        private static void SetStrictMode(TestEnvironment environment, bool enabled)
        {
            environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION",
                enabled ? null : ChangeWaves.Wave18_12.ToString());
            ChangeWaves.ResetStateForTests();
        }

        private static BuildParameters CreateStrictWarningParameters(MockLogger logger, string policy, string code = "MSB4287")
        {
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                ShutdownInProcNodeOnBuildFinish = true,
                EnableNodeReuse = false,
                UseSynchronousLogging = false,
                Loggers = [logger],
            };

            switch (policy)
            {
                case "Default":
                    break;
                case "PromoteCode":
                    parameters.WarningsAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    break;
                case "PromoteAll":
                    parameters.WarningsAsErrors = new HashSet<string>();
                    break;
                case "Suppress":
                    parameters.WarningsAsMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    break;
                case "SuppressPromoted":
                    parameters.WarningsAsErrors = new HashSet<string>();
                    parameters.WarningsAsMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    break;
                case "ExcludeFromAll":
                    parameters.WarningsAsErrors = new HashSet<string>();
                    parameters.WarningsNotAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    break;
                case "ExcludeSpecific":
                    parameters.WarningsAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    parameters.WarningsNotAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
                    break;
                case "PromoteOther":
                    parameters.WarningsAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OTHER0001" };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }

            return parameters;
        }

        private static BuildEventArgs AssertStrictDiagnostic(MockLogger logger, bool promoted, bool suppressed, string code = "MSB4287")
        {
            logger.Errors.Count.ShouldBe(promoted ? 1 : 0);
            logger.Warnings.Count.ShouldBe(!promoted && !suppressed ? 1 : 0);
            List<BuildMessageEventArgs> messages = logger.BuildMessageEvents.FindAll(e => e.Code == code);
            messages.Count.ShouldBe(suppressed ? 1 : 0);
            if (promoted)
            {
                BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();
                error.Code.ShouldBe(code);
                return error;
            }

            if (suppressed)
            {
                BuildMessageEventArgs message = messages.ShouldHaveSingleItem();
                message.Importance.ShouldBe(MessageImportance.Low);
                return message;
            }

            BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
            warning.Code.ShouldBe(code);
            return warning;
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

        private sealed class InitializationCallbackLogger(Action initialize, Action<IEventSource>? subscribe = null) : ILogger
        {
            public LoggerVerbosity Verbosity { get; set; }
            public string? Parameters { get; set; }
            public void Initialize(IEventSource eventSource)
            {
                initialize();
                subscribe?.Invoke(eventSource);
            }
            public void Shutdown() { }
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
        public bool RelativeFileExists => File.Exists("late-output.txt");

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
                else if (Violation == "Delete")
                {
                    File.Delete("late-output.txt");
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
