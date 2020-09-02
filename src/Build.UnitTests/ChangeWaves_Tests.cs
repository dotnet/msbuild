using System;
using System.Collections.Generic;
using Xunit;
using Shouldly;
using Microsoft.Build.Utilities;
using System.Threading;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.Engine.UnitTests
{
    sealed public class ChangeWaves_Tests
    {
        [Theory]
        [InlineData("16.8")]
        [InlineData("16.10")]
        [InlineData("17.0")]
        public void NoChangeWaveSetMeansAllChangeWavesAreEnabled(string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                // intentionally avoid setting MSBUILDCHANGEWAVE environment variable
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData("16.8", "test")]
        [InlineData("16.8", "    ")]
        [InlineData("16.8", "")]
        [InlineData("16.8", "16-7")]
        [InlineData("16.8", "16x7")]
        [InlineData("16.8", "16=7")]
        public void InvalidFormatDoesNotDisableChangeWaves(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVE", enabledWave);
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData ("16.8", "16.6")]
        [InlineData("16.8", "16.7")]
        public void DetermineEnabledChangeWaves(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVE", enabledWave);
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData("16.8", "16.8")]
        [InlineData("16.8", "16.10")]
        [InlineData("16.8", "17.0")]
        public void DetermineDisabledChangeWaves(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVE", enabledWave);
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeFalse();
            }
        }
    }
}
