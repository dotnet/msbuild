// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Shouldly;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Xunit.Abstractions;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Engine.UnitTests
{
    sealed public class ChangeWaves_Tests
    {
        ITestOutputHelper _output;
        public ChangeWaves_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("16.8")]
        [InlineData("16.10")]
        [InlineData("17.0")]
        [InlineData("25.87")]
        [InlineData("102.87")]
        public void EnableAllFeaturesBehindChangeWavesEnablesAllFeaturesBehindChangeWaves(string featureWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(ChangeWaves.EnableAllFeatures);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                ChangeWaves.AreFeaturesEnabled(featureWave).ShouldBe(true);

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureWave}'))\">" +
                            $"<Message Text='Hello World!'/>" +
                        $"</Target>" +
                    $"</Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }

        [Theory]
        [InlineData("16.8")]
        [InlineData("16.10")]
        [InlineData("17.0")]
        [InlineData("27.3")]
        public void NoChangeWaveSetMeansAllChangeWavesAreEnabled(string featureWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                ChangeWaves.ResetStateForTests();
                ChangeWaves.AreFeaturesEnabled(featureWave).ShouldBe(true);

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureWave}'))\">" +
                            $"<Message Text='Hello World!'/>" +
                        $"</Target>" +
                    $"</Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }

        [Theory]
        [InlineData("test")]
        [InlineData("    ")]
        [InlineData("")]
        [InlineData("16-7")]
        [InlineData("16x7")]
        [InlineData("16=7")]
        public void InvalidCallerForIsFeatureEnabledThrows(string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave("16.8");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                Shouldly.Should.Throw<InternalErrorException>(() => ChangeWaves.AreFeaturesEnabled(waveToCheck));
            }
        }

        [Theory]
        [InlineData("test", "16.8")]
        [InlineData("16_8", "5.7")]
        [InlineData("16x8", "20.4")]
        [InlineData("garbage", "18.20")]
        public void InvalidFormatThrowsWarningAndLeavesFeaturesEnabled(string disableFromWave, string featureWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(disableFromWave);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                ChangeWaves.AreFeaturesEnabled(featureWave).ShouldBe(true);

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureWave}'))\">" +
                            $"<Message Text='Hello World!'/>" +
                        $"</Target>" +
                    $"</Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                Project p = collection.LoadProject(file.Path);
                p.Build().ShouldBeTrue();

                log.WarningCount.ShouldBe(1);
                log.AssertLogContains("invalid format");
                log.AssertLogContains("Hello World!");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }

        [Theory]
        [InlineData("0.8")]
        [InlineData("4.5")]
        [InlineData("10.0")]
        public void VersionTooLowClampsToLowestVersionInRotation(string disableFromWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(disableFromWave);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                // All waves should be disabled
                for (int i = 0; i < ChangeWaves.AllWaves.Length; i++)
                {
                    ChangeWaves.ResetStateForTests();
                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.LowestWave}' and $([MSBuild]::AreFeaturesEnabled('{ChangeWaves.AllWaves[i]}')) == false\">" +
                                $"<Message Text='Hello World!'/>" +
                            $"</Target>" +
                        $"</Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                    log.WarningCount.ShouldBe(1);
                    log.AssertLogContains("out of rotation");
                    log.AssertLogContains("Hello World!");
                }

            }
        }

        [Theory]
        [InlineData("100.10")]
        [InlineData("203.45")]
        public void VersionTooHighClampsToHighestVersionInRotation(string disableFromWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(disableFromWave);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                // all waves but the highest should pass
                for (int i = 0; i < ChangeWaves.AllWaves.Length-1; i++)
                {
                    ChangeWaves.ResetStateForTests();
                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.HighestWave}' and $([MSBuild]::AreFeaturesEnabled('{ChangeWaves.AllWaves[i]}'))\">" +
                                $"<Message Text='Hello World!'/>" +
                            $"</Target>" +
                        $"</Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                    log.WarningCount.ShouldBe(1);
                    log.AssertLogContains("out of rotation");
                    log.AssertLogContains("Hello World!");
                }
            }
        }

        [Fact]
        public void VersionSetToValidValueButInvalidVersionSetsNextVersion()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave($"{ChangeWaves.LowestWaveAsVersion.Major}.{ChangeWaves.LowestWaveAsVersion.Minor}.{ChangeWaves.LowestWaveAsVersion.Build+1}");
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                // All waves should be disabled
                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.AllWaves[1]}' and $([MSBuild]::AreFeaturesEnabled('{ChangeWaves.LowestWave}'))\">" +
                                $"<Message Text='Hello World!'/>" +
                            $"</Target>" +
                        $"</Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                    log.AssertLogContains("Hello World!");

            }
        }

        [Fact]
        public void CorrectlyDetermineEnabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(ChangeWaves.HighestWave);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                for (int i = 0; i < ChangeWaves.AllWaves.Length-1; i++)
                {
                    ChangeWaves.DisabledWave = null;
                    ChangeWaves.AreFeaturesEnabled(ChangeWaves.AllWaves[i]).ShouldBe(true);

                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"$([MSBuild]::AreFeaturesEnabled('{ChangeWaves.AllWaves[i]}'))\">" +
                                $"<Message Text='Hello World!'/>" +
                            $"</Target>" +
                        $"</Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                    log.AssertLogContains("Hello World!");
                }
            }
        }

        [Fact]
        public void CorrectlyDetermineDisabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave(ChangeWaves.LowestWave);
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                foreach (string wave in ChangeWaves.AllWaves)
                {
                    ChangeWaves.AreFeaturesEnabled(wave).ShouldBeFalse();

                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"$([MSBuild]::AreFeaturesEnabled('{wave}')) == false\">" +
                                $"<Message Text='Hello World!'/>" +
                            $"</Target>" +
                        $"</Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    log.AssertLogContains("Hello World!");
                }
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }
        }
    }
}
