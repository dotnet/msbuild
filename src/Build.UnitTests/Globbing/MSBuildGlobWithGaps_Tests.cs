// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Linq;
using Microsoft.Build.Globbing;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    [TestClass]
    public class MSBuildGlobWithGaps_Tests
    {
        [MSBuildTestMethod]
        public void GlobWithGapsShouldWorkWithNoGaps()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), Enumerable.Empty<IMSBuildGlob>());

            Assert.IsTrue(glob.IsMatch("ab"));
        }

        [MSBuildTestMethod]
        public void GlobWithGapsShouldMatchIfNoGapsMatch()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), MSBuildGlob.Parse("b*"));

            Assert.IsTrue(glob.IsMatch("ab"));
        }

        [MSBuildTestMethod]
        public void GlobWithGapsShouldNotMatchIfGapsMatch()
        {
            var glob = new MSBuildGlobWithGaps(MSBuildGlob.Parse("a*"), MSBuildGlob.Parse("*b"));

            Assert.IsFalse(glob.IsMatch("ab"));
        }
    }
}
