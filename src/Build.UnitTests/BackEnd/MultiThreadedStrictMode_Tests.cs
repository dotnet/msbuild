// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
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

            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();
                MultiThreadedStrictModeScope.ActiveScope.ShouldBe(scope);

                // Compare leaf names: on Unix the directory is entered through a symlinked temporary folder.
                Path.GetFileName(Directory.GetCurrentDirectory())
                    .ShouldBe(MultiThreadedStrictModeScope.SentinelDirectoryName);

                Directory.EnumerateFileSystemEntries(scope!.SentinelDirectory).ShouldBeEmpty();
            }
            finally
            {
                scope?.Exit();
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

            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();
                MultiThreadedStrictModeScope.TryEnter(loggingService: null).ShouldBeNull();
            }
            finally
            {
                scope?.Exit();
                scope?.Exit();
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
            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();
                scope!.DetectViolations().Any.ShouldBeFalse();

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
                scope?.Exit();
            }
        }

        /// <summary>
        /// Detection has to be deterministic to be worth anything: the whole point of the mode is to remove
        /// load-dependent flakiness, so a stray write must be reported on the very next verification, every time.
        /// </summary>
        [Fact]
        public void UnresolvedPathWriteIsDetectedEveryTime()
        {
            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();

                for (int i = 0; i < 50; i++)
                {
                    string name = $"stray{i}.txt";
                    File.WriteAllText(name, "probe");

                    scope!.DetectViolations().UnresolvedPathWrites.ShouldBe(name, $"iteration {i}");
                }
            }
            finally
            {
                scope?.Exit();
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

            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();

                for (int i = 0; i < StrayCount; i++)
                {
                    File.WriteAllText($"stray{i:D2}.txt", "probe");
                }

                HashSet<string> reported = new(StringComparer.Ordinal);

                // Every verification reports at most ten names, so the whole set needs three of them.
                for (int i = 0; i < 3; i++)
                {
                    string? batch = scope!.DetectViolations().UnresolvedPathWrites;
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
                scope!.DetectViolations().Any.ShouldBeFalse();
            }
            finally
            {
                scope?.Exit();
            }
        }


        [Fact]
        public void CurrentDirectoryChangeIsDetectedOnceAndRepaired()
        {
            string originalDirectory = Directory.GetCurrentDirectory();

            MultiThreadedStrictModeScope? scope = MultiThreadedStrictModeScope.TryEnter(loggingService: null);

            try
            {
                scope.ShouldNotBeNull();

                Directory.SetCurrentDirectory(originalDirectory);

                MultiThreadedStrictModeScope.Violations violations = scope!.DetectViolations();
                violations.UnexpectedCurrentDirectory.ShouldNotBeNull();

                // Repaired, so the rest of the build keeps the protection it asked for.
                Path.GetFileName(Directory.GetCurrentDirectory())
                    .ShouldBe(MultiThreadedStrictModeScope.SentinelDirectoryName);

                scope.DetectViolations().Any.ShouldBeFalse();
            }
            finally
            {
                scope?.Exit();
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
    }
}
