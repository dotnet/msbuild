using System;
using System.Collections.Generic;
using Xunit;
using Shouldly;
using Microsoft.Build.Utilities;
using System.Threading;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Xunit.Abstractions;

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
        public void EnableAllFeaturesBehindChangeWavesEnablesAllFeaturesBehindChangeWaves(string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", ChangeWaves.EnableAllFeaturesBehindChangeWaves);
                ChangeWaves.IsFeatureEnabled(waveToCheck).ShouldBe(true);
            }
        }
        [Theory]
        [InlineData("16.8")]
        [InlineData("16.10")]
        [InlineData("17.0")]
        public void NoChangeWaveSetMeansAllChangeWavesAreEnabled(string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                // intentionally avoid setting MSBUILDCHANGEWAVE environment variable
                ChangeWaves.IsFeatureEnabled(waveToCheck).ShouldBe(true);
            }
        }

        [Theory]
        [InlineData("16.8", "test")]
        [InlineData("16.8", "    ")]
        [InlineData("16.8", "")]
        [InlineData("16.8", "16-7")]
        [InlineData("16.8", "16x7")]
        [InlineData("16.8", "16=7")]
        public void InvalidCallerFormatThrows(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);
                Shouldly.Should.Throw(() => ChangeWaves.IsFeatureEnabled(waveToCheck), typeof(InternalErrorException));
            }
        }

        [Theory]
        [InlineData("test", "16.8")]
        [InlineData("16-8", "16.8")]
        [InlineData("16x8", "16.8")]
        [InlineData("garbage", "18.20")]
        [InlineData("", "15.6")]
        public void InvalidUserValueThrowsWarningAndLeavesWavesEnabled(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);

                string project = @"
            <Project>
                <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionGreaterThan('$(MSBUILDCHANGEWAVEVERSION)', '" + waveToCheck + @"'))"">
                    <Message Text='Hello World!'/>
                </Target>
                <PropertyGroup>
                        <msbuildchangewaveversion>" + enabledWave + @"</msbuildchangewaveversion>
                </PropertyGroup>
            </Project>";

                MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(project);

                ChangeWaves.IsFeatureEnabled(waveToCheck).ShouldBe(true);
                log.AssertLogContains("Warning");
            }
        }

        [Theory]
        [InlineData ("16.8", "16.6")]
        [InlineData("16.8", "16.7")]
        [InlineData("16.11", "16.10")]
        public void DetermineEnabledChangeWaves(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);
                ChangeWaves.IsFeatureEnabled(waveToCheck).ShouldBe(true);
            }
        }

        [Theory]
        [InlineData("16.8", "16.6")]
        [InlineData("16.8", "16.7")]
        [InlineData("16.11", "16.10")]
        public void DetermineEnabledChangeWaves_InProjectFile(string enabledWave, string waveToCheck)
        {
            string project = @"
            <Project>
                <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionGreaterThan('$(MSBUILDCHANGEWAVEVERSION)', '" + waveToCheck + @"'))"">
                    <Message Text='Hello World!'/>
                </Target>
                <PropertyGroup>
                        <msbuildchangewaveversion>" + enabledWave + @"</msbuildchangewaveversion>
                </PropertyGroup>
            </Project>";

            MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(project);
            log.AssertLogContains("Hello World");
        }

        [Theory]
        [InlineData("16.8", "16.8")]
        [InlineData("16.8", "16.10")]
        [InlineData("16.8", "17.0")]
        [InlineData("16.11", "16.12")]
        public void DetermineDisabledChangeWaves(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", enabledWave);
                ChangeWaves.IsFeatureEnabled(waveToCheck).ShouldBe(true);
            }
        }

        [Theory]
        [InlineData("16.8", "16.8")]
        [InlineData("16.8", "16.10")]
        [InlineData("16.8", "17.0")]
        [InlineData("16.11", "16.12")]
        public void DetermineDisabledChangeWaves_InProjectFile(string enabledWave, string waveToCheck)
        {
            string project = @"
            <Project>
                <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionGreaterThan('$(MSBUILDCHANGEWAVEVERSION)', '" + waveToCheck + @"'))"">
                    <Message Text='Hello World!'/>
                </Target>
                <PropertyGroup>
                        <msbuildchangewaveversion>" + enabledWave + @"</msbuildchangewaveversion>
                </PropertyGroup>
            </Project>";

            MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(project);
            log.AssertLogDoesntContain("Hello World");
        }


    }


}
