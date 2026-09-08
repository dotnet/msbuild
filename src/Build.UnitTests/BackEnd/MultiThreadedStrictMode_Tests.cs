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
    /// Tests for the opt-in multi-threaded strict mode described in
    /// https://github.com/dotnet/msbuild/issues/14794.
    /// </summary>
    public class MultiThreadedStrictMode_Tests
    {
        private readonly ITestOutputHelper _output;

        public MultiThreadedStrictMode_Tests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void BuildParametersClonePreservesMultiThreadedStrict()
        {
            BuildParameters parameters = new() { MultiThreaded = true, MultiThreadedStrict = true };

            BuildParameters clone = parameters.Clone();

            clone.MultiThreadedStrict.ShouldBeTrue();
        }

        [Fact]
        public void BuildParametersTranslationPreservesMultiThreadedStrict()
        {
            BuildParameters parameters = new() { MultiThreaded = true, MultiThreadedStrict = true };

            ((ITranslatable)parameters).Translate(TranslationHelpers.GetWriteTranslator());
            BuildParameters deserialized = BuildParameters.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            deserialized.MultiThreaded.ShouldBeTrue();
            deserialized.MultiThreadedStrict.ShouldBeTrue();
        }

        /// <summary>
        /// The scope owns process-wide state, so entering must move the process and exiting must put it back.
        /// </summary>
        [Fact]
        public void ScopeMovesAndRestoresCurrentDirectory()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

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
        }

        /// <summary>
        /// Only one scope may own the process current directory, and exiting twice must not disturb whoever owns
        /// it next.
        /// </summary>
        [Fact]
        public void SecondScopeIsRejectedAndExitIsIdempotent()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

            try
            {
                Should.Throw<InvalidOperationException>(() => MultiThreadedStrictModeScope.Enter());
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
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

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
            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

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

        /// <summary>
        /// More stray entries than fit in one diagnostic must be truncated, not dropped: the remainder has to
        /// show up in later verifications.
        /// </summary>
        [Fact]
        public void UnresolvedPathWritesAreTruncatedButNotDropped()
        {
            const int StrayCount = 25;

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

            try
            {
                for (int i = 0; i < StrayCount; i++)
                {
                    File.WriteAllText($"stray{i:D2}.txt", "probe");
                }

                HashSet<string> reported = new(StringComparer.Ordinal);

                // Every verification reports at most ten names, so the whole set needs three of them.
                for (int i = 0; i < 3; i++)
                {
                    string? batch = scope.DetectViolations().UnresolvedPathWrites;
                    batch.ShouldNotBeNull();

                    foreach (string name in batch!.Split([", "], StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (name != "...")
                        {
                            reported.Add(name).ShouldBeTrue($"{name} was reported twice");
                        }
                    }
                }

                reported.Count.ShouldBe(StrayCount);
                scope.DetectViolations().Any.ShouldBeFalse();
            }
            finally
            {
                scope.Exit();
            }
        }


        [Fact]
        public void CurrentDirectoryChangeIsDetectedOnceAndRepaired()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope scope = MultiThreadedStrictModeScope.Enter();

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

        /// <summary>
        /// Strict mode is meaningless outside multi-threaded mode, so a build that is not multi-threaded must not
        /// have the process moved out from under it even when the opt-in is present.
        /// </summary>
        [Fact]
        public void StrictModeIsIgnoredWhenBuildIsNotMultiThreaded()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMULTITHREADEDSTRICT", "1");

            string originalDirectory = Directory.GetCurrentDirectory();

            BuildParameters parameters = new()
            {
                MultiThreaded = false,
                MultiThreadedStrict = true,
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
            env.SetEnvironmentVariable("MSBUILDMULTITHREADEDSTRICT", null);
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
                MultiThreadedStrict = strict,
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
        public void OrdinaryMtRespectsSaveOperatingEnvironment(bool saveEnvironment)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDMULTITHREADEDSTRICT", null);
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
            MultiThreadedStrictModeScope first = MultiThreadedStrictModeScope.Enter();
            using var firstLifetime = new ScopeLifetime(first);
            string file = Path.Combine(first.SentinelDirectory, "locked.txt");
            using FileStream lockedFile = new(file, FileMode.Create, System.IO.FileAccess.ReadWrite, FileShare.Read);
            first.Exit();

            MultiThreadedStrictModeScope second = MultiThreadedStrictModeScope.Enter();
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
            MultiThreadedStrictModeScope first = MultiThreadedStrictModeScope.Enter();
            using var firstLifetime = new ScopeLifetime(first);
            MultiThreadedStrictModeScope? second = null;
            Exception? threadException = null;
            using ManualResetEventSlim started = new();
            Thread thread = new(() =>
            {
                started.Set();
                try
                {
                    second = MultiThreadedStrictModeScope.Enter();
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
            env.SetEnvironmentVariable("MSBUILDMULTITHREADEDSTRICT", null);
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
            logger.AssertLogContains(violation == "Write" ? "MSB4287" : "MSB4286");
            logger.AssertLogDoesntContain("MSB4181");
            logger.TaskFinishedEvents.Find(e => e.TaskName == nameof(StrictLifetimeTask))!.Succeeded.ShouldBeFalse();
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

        [Fact]
        public void CancellationDuringOutputGatheringSuppressesDeferredDiagnostics()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
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
            TaskBuilder? builder = null;
            BuildManager? activeManager = null;
            bool cancellationObserved = false;
            HostServices hostServices = new();
            hostServices.RegisterHostObject(project.Path, "Build", nameof(StrictLifetimeTask), new OutputCallbackHost(() =>
            {
                activeManager!.CancelAllSubmissions();
                var token = (CancellationToken)typeof(TaskBuilder)
                    .GetField("_cancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(builder)!;
                cancellationObserved = SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(10));
            }));
            MockLogger logger = new(_output);

            BuildResult result = BuildStrictProject(project.Path, logger, manager =>
            {
                activeManager = manager;
                ((IBuildComponentHost)manager).RegisterFactory(BuildComponentType.TaskBuilder,
                    type => builder = (TaskBuilder)TaskBuilder.CreateComponent(type));
            }, hostServices);

            cancellationObserved.ShouldBeTrue();
            result.ShouldHaveFailed();
            logger.AssertLogDoesntContain("MSB4181");
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

            Should.Throw<IOException>(() => MultiThreadedStrictModeScope.Enter());
            MultiThreadedStrictModeScope.ActiveScope.ShouldBeNull();
            Directory.GetCurrentDirectory().ShouldBe(originalDirectory);
        }

        [Fact]
        public void StrictVerificationFailureDoesNotLookClean()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string originalDirectory = Directory.GetCurrentDirectory();
            env.SetCurrentDirectory(originalDirectory);
            var scope = MultiThreadedStrictModeScope.Enter();
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
            var first = MultiThreadedStrictModeScope.Enter();
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            string outputCache = Path.Combine(env.CreateFolder().Path, "failed-entry.cache");
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                MultiThreadedStrict = true,
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
            var first = MultiThreadedStrictModeScope.Enter();
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            LoggerException shutdownFailure = new("logger shutdown failure");
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                MultiThreadedStrict = true,
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
            var first = MultiThreadedStrictModeScope.Enter();
            using var lifetime = new ScopeLifetime(first);
            using BuildManager manager = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                MultiThreadedStrict = true,
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
                MultiThreadedStrict = true,
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
                MultiThreadedStrict = true,
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
    public sealed class StrictLifetimeTask : Microsoft.Build.Utilities.Task
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

    internal sealed class OutputCallbackHost(Action callback) : ITaskHost
    {
        private Action? _callback = callback;
        public void OnOutput() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }
}
