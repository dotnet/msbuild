// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Linq;
using Microsoft.Build.Globbing;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    public class MSBuildGlobWithGaps_Tests
    {
        [Fact]
        public void GlobWithGapsShoulWorkWithNoGaps()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), Enumerable.Empty<IMSBuildGlob>());

            Assert.True(glob.IsMatch("ab"));
        }

        [Fact]
        public void GlobWithGapsShoulMatchIfNoGapsMatch()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), MSBuildGlob.Parse("b*"));

            Assert.True(glob.IsMatch("ab"));
        }

        [Fact]
        public void GlobWithGapsShoulNotMatchIfGapsMatch()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), MSBuildGlob.Parse("*b"));

            Assert.False(glob.IsMatch("ab"));
        }
    }
}