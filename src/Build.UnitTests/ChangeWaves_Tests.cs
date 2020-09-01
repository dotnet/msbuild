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
        public void NoSetChangeWaveMeansAllChangeWavesAreEnabled(string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData("16.8", "test")]
        [InlineData("16.8", "17-0")]
        [InlineData("16.8", "    ")]
        [InlineData("16.8", "")]
        public void InvalidWavesToCheckMeansWeKeepAllWavesEnabled(string enabledWave, string waveToCheck)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVE", enabledWave);
                ChangeWaves.IsChangeWaveEnabled(waveToCheck).ShouldBeTrue();
            }
        }

        [Theory]
        // food for thought: how do we scale this?
        // potential solution: iterate through the enum, parse the enum and
        //      compare it to the currentwave to determine if it should ACTUALLY be enabled?
        //      sounds like a plan.
        [InlineData ("16.8", "16.8")]
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
