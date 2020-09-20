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
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.EnableAllFeatures);
                ChangeWaves.IsChangeWaveEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '" + ChangeWaves.EnableAllFeatures + @"' and $([MSBuild]::IsChangeWaveEnabled('" + featureWave + @"'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
                ChangeWaves.DisabledWave = null;
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
                ChangeWaves.IsChangeWaveEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition="" '$(MSBUILDDISABLEFEATURESFROMVERSION)' == '" + ChangeWaves.EnableAllFeatures + @"' and $([MSBuild]::IsChangeWaveEnabled('" + featureWave + @"'))"">
                            <Message Text='Hello World!'/>
                        </Target>
                    </Project>";

                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

                ProjectCollection collection = new ProjectCollection();
                MockLogger log = new MockLogger();
                collection.RegisterLogger(log);

                collection.LoadProject(file.Path).Build().ShouldBeTrue();
                log.AssertLogContains("Hello World!");
                ChangeWaves.DisabledWave = null;
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
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "16.8");
                Shouldly.Should.Throw<InternalErrorException>(() => ChangeWaves.IsChangeWaveEnabled(waveToCheck));
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
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableFromWave);
                ChangeWaves.IsChangeWaveEnabled(featureWave).ShouldBe(true);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '" + ChangeWaves.EnableAllFeatures + @"' and $([MSBuild]::IsChangeWaveEnabled('" + featureWave + @"'))"">
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
                ChangeWaves.DisabledWave = null;
            }
        }

        [Theory]
        [InlineData("0.8")]
        [InlineData("203.45")]
        public void OutOfRotationWavesThrowsWarningAndDisablesFeatures(string disableFromWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableFromWave);

                string projectFile = @"
                    <Project>
                        <Target Name='HelloWorld' Condition=""'$(MSBUILDDISABLEFEATURESFROMVERSION)' == '" + ChangeWaves.LowestWave + @"' and $([MSBuild]::IsChangeWaveEnabled('" + ChangeWaves.LowestWave + @"')) == 'false'"">
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
                log.AssertLogContains("Hello World!");
                ChangeWaves.DisabledWave = null;
            }
        }

        [Fact]
        public void CorrectlyDetermineEnabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.HighestWave);

                for (int i = 0; i < ChangeWaves.AllWaves.Length-1; i++)
                {
                    ChangeWaves.IsChangeWaveEnabled(ChangeWaves.AllWaves[i]).ShouldBe(true);

                    string projectFile = @"
                        <Project>
                            <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionLessThan('" + ChangeWaves.AllWaves[i] + @"', '$(MSBUILDDISABLEFEATURESFROMVERSION)'))"">
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
                    ChangeWaves.DisabledWave = null;
                }
            }
        }

        [Fact]
        public void CorrectlyDetermineDisabledFeatures()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.LowestWave);

                foreach (string wave in ChangeWaves.AllWaves)
                {
                    ChangeWaves.IsChangeWaveEnabled(wave).ShouldBeFalse();

                    string projectFile = @"
                        <Project>
                            <Target Name='HelloWorld' Condition=""$([MSBuild]::IsChangeWaveEnabled('" + wave + @"')) == 'false'"">
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
                    ChangeWaves.DisabledWave = null;
                }
            }
        }
    }
}
