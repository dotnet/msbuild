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
        // Every test:
        // out of rotation-specific tests
        // 

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
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", ChangeWaves.EnableAllFeaturesBehindChangeWaves);
                ChangeWaves.IsFeatureEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
            <Project>
                <Target Name='HelloWorld' Condition=""'$(MSBUILDCHANGEWAVEVERSION)' == '999.999' and $([MSBuild]::VersionLessThan('" + featureWave + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                    <Message Text='Hello World!'/>
                </Target>
            </Project>";

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
        public void NoChangeWaveSetMeansAllChangeWavesAreEnabled(string featureWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                ChangeWaves.IsFeatureEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition="" '$(MSBUILDCHANGEWAVEVERSION)' == '999.999' and $([MSBuild]::VersionLessThan('" + featureWave + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
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
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", "16.8");
                Shouldly.Should.Throw(() => ChangeWaves.IsFeatureEnabled(waveToCheck), typeof(InternalErrorException));
            }
        }

        [Theory]
        [InlineData("test", "16.8")]
        [InlineData("16_8", "5.7")]
        [InlineData("16x8", "20.4")]
        [InlineData("garbage", "18.20")]
        public void InvalidFormatThrowsWarningAndLeavesFeaturesEnabled(string enabledWave, string featureWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);
                ChangeWaves.IsFeatureEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""'$(MSBUILDCHANGEWAVEVERSION)' == '" + ChangeWaves.EnableAllFeaturesBehindChangeWaves + @"' and $([MSBuild]::VersionLessThan('" + featureWave + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                Project p = collection.LoadProject(file.Path);
                p.Build().ShouldBeTrue();

                log.AssertLogContains("invalid format");
                log.AssertLogContains("Hello World!");
            }
        }

        [Theory]
        [InlineData("0.8")]
        [InlineData("203.45")]
        public void OutOfRotationWavesThrowsWarningAndDisablesFeatures(string enabledWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""'$(MSBUILDCHANGEWAVEVERSION)' == '" + ChangeWaves.LowestWave + @"' and $([MSBuild]::VersionLessThan('" + ChangeWaves.LowestWave + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                Project p = collection.LoadProject(file.Path);
                p.Build().ShouldBeTrue();

                log.AssertLogContains("out of rotation");
                log.AssertLogDoesntContain("Hello World!");
            }
        }

        /// <summary>
        /// When MSBUILDCHANGEWAVEVERSION is set, any feature in a wave lower than that will be enabled.
        /// </summary>
        /// <param name="featureWave"></param>
        [Fact]
        public void CorrectlyDetermineEnabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", ChangeWaves.HighestWave);

                for (int i = 0; i < ChangeWaves.AllWaves.Length-1; i++)
                {
                    ChangeWaves.IsFeatureEnabled(ChangeWaves.AllWaves[i]).ShouldBe(true);

                    string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionLessThan('" + ChangeWaves.AllWaves[i] + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    log.AssertLogContains("Hello World!");
                }
            }
        }

        [Fact]
        public void CorrectlyDetermineDisabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", ChangeWaves.LowestWave);

                foreach (string wave in ChangeWaves.AllWaves)
                {
                    ChangeWaves.IsFeatureEnabled(wave).ShouldBeFalse();

                    string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionLessThan('" + wave + @"', '$(MSBUILDCHANGEWAVEVERSION)'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                    ProjectCollection collection = new ProjectCollection();
                    MockLogger log = new MockLogger();
                    collection.RegisterLogger(log);

                    Project p = collection.LoadProject(file.Path);
                    p.Build().ShouldBeTrue();

                    log.AssertLogDoesntContain("Hello World!");
                }
            }
        }
    }
}
