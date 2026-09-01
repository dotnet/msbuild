// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
