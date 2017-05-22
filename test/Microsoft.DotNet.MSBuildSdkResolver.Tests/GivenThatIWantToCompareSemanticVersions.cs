// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Microsoft.DotNet.MSBuildSdkResolver;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToCompareFXVersions
    {
        [Theory]
        [InlineData("2.0.0", "1.0.0", 1)]
        [InlineData("1.1.0", "1.0.0", 1)]
        [InlineData("1.0.1", "1.0.0", 1)]
        [InlineData("1.0.0", "1.0.0-pre", 1)]
        [InlineData("1.0.0-pre+2", "1.0.0-pre+1", 1)]
        [InlineData("1.0.0", "2.0.0", -1)]
        [InlineData("1.0.0", "1.1.0", -1)]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("1.0.0-pre", "1.0.0", -1)]
        [InlineData("1.0.0-pre+1", "1.0.0-pre+2", -1)]
        [InlineData("1.2.3", "1.2.3", 0)]
        [InlineData("1.2.3-pre", "1.2.3-pre", 0)]
        [InlineData("1.2.3-pre+1", "1.2.3-pre+1", 0)]
        public void OneFXVersionIsBiggerThanTheOther(string s1, string s2, int expectedResult)
        {
            FXVersion fxVersion1;
            FXVersion fxVersion2;
            FXVersion.TryParse(s1, out fxVersion1);
            FXVersion.TryParse(s2, out fxVersion2);
            FXVersion.Compare(fxVersion1, fxVersion2).Should().Be(expectedResult);
        }
    }
}