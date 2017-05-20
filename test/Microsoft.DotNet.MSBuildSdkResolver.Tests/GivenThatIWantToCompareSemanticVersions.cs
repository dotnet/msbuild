// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Microsoft.DotNet.MSBuildSdkResolver;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToCompareSemanticVersions
    {
        [Theory]
        [InlineData("2.0.0", "1.0.0")]
        [InlineData("1.1.0", "1.0.0")]
        [InlineData("1.0.1", "1.0.0")]
        [InlineData("1.0.0", "1.0.0-pre")]
        [InlineData("1.0.0-pre+2", "1.0.0-pre+1")]
        public void OneSemanticVersionIsBiggerThanTheOther(string s1, string s2)
        {
            var biggerThan = SemanticVersion.Parse(s1) > SemanticVersion.Parse(s2);
            var smallerThan = SemanticVersion.Parse(s1) < SemanticVersion.Parse(s2);
            biggerThan.Should().BeTrue();
            smallerThan.Should().BeFalse();
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0")]
        [InlineData("1.0.0", "1.1.0")]
        [InlineData("1.0.0", "1.0.1")]
        [InlineData("1.0.0-pre", "1.0.0")]
        [InlineData("1.0.0-pre+1", "1.0.0-pre+2")]
        public void OneSemanticVersionIsSmallerThanTheOther(string s1, string s2)
        {
            var smallerThan = SemanticVersion.Parse(s1) < SemanticVersion.Parse(s2);
            var biggerThan = SemanticVersion.Parse(s1) > SemanticVersion.Parse(s2);
            smallerThan.Should().BeTrue();
            biggerThan.Should().BeFalse();
        }

        [Theory]
        [InlineData("2.0.0", "1.0.0")]
        [InlineData("1.0.0", "1.0.0")]
        public void OneSemanticVersionIsBiggerThanOrEqualsTheOther(string s1, string s2)
        {
            var biggerThanOrEquals = SemanticVersion.Parse(s1) >= SemanticVersion.Parse(s2);
            biggerThanOrEquals.Should().BeTrue();
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0")]
        [InlineData("1.0.0", "1.0.0")]
        public void OneSemanticVersionIsSmallerThanOrEqualsTheOther(string s1, string s2)
        {
            var smallerThanOrEquals = SemanticVersion.Parse(s1) <= SemanticVersion.Parse(s2);
            smallerThanOrEquals.Should().BeTrue();
        }

        [Theory]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.3-pre", "1.2.3-pre")]
        [InlineData("1.2.3-pre+1", "1.2.3-pre+1")]
        public void SemanticVersionsCanBeEqual(string s1, string s2)
        {
            var equals = SemanticVersion.Parse(s1) == SemanticVersion.Parse(s2);
            var different = SemanticVersion.Parse(s1) != SemanticVersion.Parse(s2);
            equals.Should().BeTrue();
            different.Should().BeFalse();
        }

        [Theory]
        [InlineData("1.2.3", "1.2.0")]
        [InlineData("1.2.3-pre", "1.2.3-pra")]
        [InlineData("1.2.3-pre+1", "1.2.3-pre+2")]
        public void SemanticVersionsCanBeDifferent(string s1, string s2)
        {
            var different = SemanticVersion.Parse(s1) != SemanticVersion.Parse(s2);
            var equals = SemanticVersion.Parse(s1) == SemanticVersion.Parse(s2);
            different.Should().BeTrue();
            equals.Should().BeFalse();
        }
    }
}