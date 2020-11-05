// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;
using Shouldly;
using Microsoft.Build.Utilities;
using System.Threading;
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
        public void EnableAllFeaturesBehindChangeWavesEnablesAllFeaturesBehindChangeWaves(string featureVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                Version featureAsVersion = Version.Parse(featureVersion);
                env.SetChangeWave(ChangeWaves.EnableAllFeatures);
                ChangeWaves.AreFeaturesEnabled(featureAsVersion).ShouldBe(true);

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureVersion}'))\">" +
                            $"<Message Text='Hello World!'/>" +
                        $"</Target>" +
                    $"</Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
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
                Version featureAsVersion = Version.Parse(featureVersion);
                ChangeWaves.AreFeaturesEnabled(featureAsVersion).ShouldBe(true);

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureVersion}'))\">" +
                            $"<Message Text='Hello World!'/>" +
                        $"</Target>" +
                    $"</Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
            }
        }

        [Theory]
        [InlineData("test", "16.8")]
        [InlineData("16_8", "5.7")]
        [InlineData("16x8", "20.4")]
        [InlineData("garbage", "18.20")]
        public void InvalidFormatThrowsWarningAndLeavesFeaturesEnabled(string disableFeaturesFromVersion, string featureVersion)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                Version featureAsVersion = Version.Parse(featureVersion);
                env.SetChangeWave(disableFeaturesFromVersion);
                ChangeWaves.AreFeaturesEnabled(featureAsVersion).ShouldBeTrue();

                string projectFile = $"" +
                    $"<Project>" +
                        $"<Target Name='HelloWorld' Condition=\"'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '{ChangeWaves.EnableAllFeatures}' and $([MSBuild]::AreFeaturesEnabled('{featureVersion}'))\">" +
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
                log.AssertLogContains("MSB4271");
                log.AssertLogContains("Hello World!");
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
                env.SetChangeWave(disableFeaturesFromVersion);

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

                    log.WarningCount.ShouldBe(1);
                    log.AssertLogContains("MSB4272");
                    log.AssertLogContains("Hello World!");
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
                env.SetChangeWave(disableFeaturesFromVersion);

                // all waves but the highest should pass
                for (int i = 0; i < ChangeWaves.AllWaves.Length - 1; i++)
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

                    log.WarningCount.ShouldBe(1);
                    log.AssertLogContains("MSB4272");
                    log.AssertLogContains("Hello World!");
                }
            }
        }

        [Fact]
        public void VersionSetToValidValueButInvalidVersionSetsNextVersion()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetChangeWave($"{ChangeWaves.LowestWave.Major}.{ChangeWaves.LowestWave.Minor}.{ChangeWaves.LowestWave.Build + 1}");

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

                for (int i = 0; i < ChangeWaves.AllWaves.Length - 1; i++)
                {
                    ChangeWaves.ResetStateForTests();
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

                foreach (Version wave in ChangeWaves.AllWaves)
                {
                    ChangeWaves.ResetStateForTests();
                    ChangeWaves.AreFeaturesEnabled(wave).ShouldBeFalse();

                    string projectFile = $"" +
                        $"<Project>" +
                            $"<Target Name='HelloWorld' Condition=\"$([MSBuild]::AreFeaturesEnabled('{wave.ToString()}')) == false\">" +
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
