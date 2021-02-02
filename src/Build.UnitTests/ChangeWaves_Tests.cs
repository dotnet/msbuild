// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Shouldly;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities;
using Microsoft.Build.UnitTests;
using Xunit.Abstractions;
using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Engine.UnitTests
{
    sealed public class ChangeWaves_Tests
    {
        ITestOutputHelper _output;
        public ChangeWaves_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Performs necessary operations for setting the MSBuildDisableFeaturesFromVersion environment variable.
        /// This is required because Change Waves is static and stale values can be seen between tests in the same assembly.
        /// </summary>
        /// <param name="wave">The version to set as the current Change Wave.</param>
        private void SetChangeWave(string wave, TestEnvironment env)
        {
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", wave);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
        }

        /// <summary>
        /// Helper function to build a simple project based on a particular change wave being set.
        /// Call SetChangeWave on your TestEnvironment before calling this function.
        /// </summary>
        /// <param name="testEnvironment">The TestEnvironment being used for this test.</param>
        /// <param name="versionToCheckAgainstCurrentChangeWave">The version to compare to the current set Change Wave.</param>
        /// <param name="currentChangeWaveShouldUltimatelyResolveTo">What the project property for the environment variable MSBuildDisableFeaturesFromVersion ultimately resolves to.</param>
        /// <param name="warningCodesLogShouldContain">An array of warning codes that should exist in the resulting log. Ex: "MSB4271".</param>
        private void buildSimpleProjectAndValidateChangeWave(TestEnvironment testEnvironment, Version versionToCheckAgainstCurrentChangeWave, Version currentChangeWaveShouldUltimatelyResolveTo, params string[] warningCodesLogShouldContain)
        {
            bool isThisWaveEnabled = versionToCheckAgainstCurrentChangeWave < currentChangeWaveShouldUltimatelyResolveTo || currentChangeWaveShouldUltimatelyResolveTo == ChangeWaves.EnableAllFeatures;

            ChangeWaves.AreFeaturesEnabled(versionToCheckAgainstCurrentChangeWave).ShouldBe(isThisWaveEnabled);

            string projectFile = $"" +
                $"<Project>" +
                    $"<Target Name='HelloWorld' Condition=\"$([MSBuild]::AreFeaturesEnabled('{versionToCheckAgainstCurrentChangeWave}')) and '$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{currentChangeWaveShouldUltimatelyResolveTo}'\">" +
                        $"<Message Text='Hello World!'/>" +
                    $"</Target>" +
                $"</Project>";

            TransientTestFile file = testEnvironment.CreateFile("proj.csproj", projectFile);

            ProjectCollection collection = new ProjectCollection();
            MockLogger log = new MockLogger(_output);
            collection.RegisterLogger(log);

            Project p = collection.LoadProject(file.Path);
            p.Build().ShouldBeTrue();

            log.FullLog.Contains("Hello World!").ShouldBe(isThisWaveEnabled);

            if (warningCodesLogShouldContain != null)
            {
                log.WarningCount.ShouldBe(warningCodesLogShouldContain.Length);
                log.AssertLogContains(warningCodesLogShouldContain);
            }

            ChangeWaves.ResetStateForTests();
        }

        [Fact]
        public void EnableAllFeaturesBehindChangeWavesEnablesAllFeaturesBehindChangeWaves()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(ChangeWaves.EnableAllFeatures.ToString(), env);

                for (int i = 0; i < ChangeWaves.AllWaves.Length - 1; i++)
                {
                    buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                            versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[i],
                                                            currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.EnableAllFeatures,
                                                            warningCodesLogShouldContain: null);
                }
            }
        }

        [Theory]
        [InlineData("16.8")]
        [InlineData("16.10")]
        [InlineData("17.0")]
        [InlineData("27.3")]
        public void NoChangeWaveSetMeansAllChangeWavesAreEnabled(string featureVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                // Reset static ChangeWave
                SetChangeWave(string.Empty, env);
                Version featureAsVersion = Version.Parse(featureVersion);
                ChangeWaves.AreFeaturesEnabled(featureAsVersion).ShouldBe(true);

                buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                        versionToCheckAgainstCurrentChangeWave: featureAsVersion,
                                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.EnableAllFeatures,
                                                        warningCodesLogShouldContain: null);
            }
        }

        [Theory]
        [InlineData("test")]
        [InlineData("16_8")]
        [InlineData("16x8")]
        [InlineData("garbage")]
        public void InvalidFormatThrowsWarningAndLeavesFeaturesEnabled(string disableFeaturesFromVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(disableFeaturesFromVersion, env);

                buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                        versionToCheckAgainstCurrentChangeWave: ChangeWaves.HighestWave,
                                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.EnableAllFeatures,
                                                        warningCodesLogShouldContain: "MSB4271");
            }
        }

        [Theory]
        [InlineData("0.8")]
        [InlineData("4.5")]
        [InlineData("10.0")]
        public void VersionTooLowClampsToLowestVersionInRotation(string disableFeaturesFromVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(disableFeaturesFromVersion, env);

                // All waves should be disabled
                for (int i = 0; i < ChangeWaves.AllWaves.Length; i++)
                {
                    buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                            versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[i],
                                                            currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.LowestWave,
                                                            warningCodesLogShouldContain: "MSB4272");
                }
            }
        }

        [Theory]
        [InlineData("100.10")]
        [InlineData("203.45")]
        public void VersionTooHighClampsToHighestVersionInRotation(string disableFeaturesFromVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(disableFeaturesFromVersion, env);

                // all waves but the highest should pass
                for (int i = 0; i < ChangeWaves.AllWaves.Length - 1; i++)
                {
                    buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                        versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[i],
                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.HighestWave,
                                        warningCodesLogShouldContain: "MSB4272");
                }

                // Make sure the last wave is disabled.
                buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                        versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[ChangeWaves.AllWaves.Length - 1],
                                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.HighestWave,
                                                        warningCodesLogShouldContain: "MSB4272");
            }
        }

        [Fact]
        public void VersionSetToValidValueButInvalidVersionSetsNextVersion()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave($"{ChangeWaves.LowestWave.Major}.{ChangeWaves.LowestWave.Minor}.{ChangeWaves.LowestWave.Build + 1}", env);

                buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                        versionToCheckAgainstCurrentChangeWave: ChangeWaves.LowestWave,
                                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.AllWaves[1],
                                                        warningCodesLogShouldContain: null);
            }
        }

        [Fact]
        public void CorrectlyDetermineEnabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(ChangeWaves.HighestWave.ToString(), env);

                for (int i = 0; i < ChangeWaves.AllWaves.Length - 1; i++)
                {
                    buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                            versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[i],
                                                            currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.HighestWave,
                                                            warningCodesLogShouldContain: null);
                }

                // Make sure the last wave is disabled.
                buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                        versionToCheckAgainstCurrentChangeWave: ChangeWaves.AllWaves[ChangeWaves.AllWaves.Length - 1],
                                                        currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.HighestWave,
                                                        warningCodesLogShouldContain: null);
            }
        }

        [Fact]
        public void CorrectlyDetermineDisabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                SetChangeWave(ChangeWaves.LowestWave.ToString(), env);

                foreach (Version wave in ChangeWaves.AllWaves)
                {
                    buildSimpleProjectAndValidateChangeWave(testEnvironment: env,
                                                            versionToCheckAgainstCurrentChangeWave: wave,
                                                            currentChangeWaveShouldUltimatelyResolveTo: ChangeWaves.LowestWave,
                                                            warningCodesLogShouldContain: null);
                }
            }
        }
    }
}
