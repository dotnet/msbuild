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
        // food for thought: how do we scale this?
        // potential solution: iterate through the enum, parse the enum and
        //      compare it to the currentwave to determine if it should ACTUALLY be enabled?
        //      sounds like a plan.
        [InlineData ("16.8", MSBuildChangeWaveVersion.v16_8)]
        [InlineData ("16.8", MSBuildChangeWaveVersion.v16_10)]
        [InlineData ("16.8", MSBuildChangeWaveVersion.v17_0)]
        public void CorrectlyDetermineEnabledChangeWaves(string currentWave, MSBuildChangeWaveVersion changeWave)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDCHANGEWAVE", currentWave);
                ChangeWaves.IsChangeWaveEnabled(changeWave).ShouldBeTrue();
            }
                
        }
    }
}
