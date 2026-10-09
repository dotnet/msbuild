// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class Traits_Tests
    {
        [Theory]
        [InlineData(null, -1)]
        [InlineData("", -1)]
        [InlineData("0", 0)]
        [InlineData("-1", -1)]
        [InlineData("garbage", -1)]
        [InlineData("1", 1)]
        [InlineData("16", 16)]
        public void CopyTaskParallelismReadsIntegerOverride(string value, int expectedParallelism)
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILDCOPYTASKPARALLELISM", value);

            Traits traits = new Traits();

            traits.CopyTaskParallelism.ShouldBe(expectedParallelism);
        }
    }
}
