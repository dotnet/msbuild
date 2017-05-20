// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Microsoft.DotNet.MSBuildSdkResolver;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToParseSemanticVersions
    {
        [Fact]
        public void ReturnsNullWhenNoMajorSeparatorIsFound()
        {
            var semanticVersion = SemanticVersion.Parse("1");

            semanticVersion.Should().BeNull();
        }

        [Fact]
        public void ReturnsNullWhenMajorPortionIsNotANumber()
        {
            var semanticVersion = SemanticVersion.Parse("a.0.0");

            semanticVersion.Should().BeNull();
        }

        [Fact]
        public void ReturnsNullWhenNoMinorSeparatorIsFound()
        {
            var semanticVersion = SemanticVersion.Parse("1.0");

            semanticVersion.Should().BeNull();
        }

        [Fact]
        public void ReturnsNullWhenMinorPortionIsNotANumber()
        {
            var semanticVersion = SemanticVersion.Parse("1.a.0");

            semanticVersion.Should().BeNull();
        }

        [Fact]
        public void ReturnsNullWhenPatchPortionIsNotANumber()
        {
            var semanticVersion = SemanticVersion.Parse("1.0.a");

            semanticVersion.Should().BeNull();
        }

        [Fact]
        public void ReturnsSemanticVersionWhenOnlyMajorMinorPatchIsFound()
        {
            var semanticVersion = SemanticVersion.Parse("1.2.3");

            semanticVersion.Should().NotBeNull();
            semanticVersion.Major.Should().Be(1);
            semanticVersion.Minor.Should().Be(2);
            semanticVersion.Patch.Should().Be(3);
        }

        [Fact]
        public void ReturnsSemanticVersionWhenOnlyMajorMinorPatchAndPreIsFound()
        {
            var semanticVersion = SemanticVersion.Parse("1.2.3-pre");

            semanticVersion.Should().NotBeNull();
            semanticVersion.Major.Should().Be(1);
            semanticVersion.Minor.Should().Be(2);
            semanticVersion.Patch.Should().Be(3);
            semanticVersion.Pre.Should().Be("-pre");
        }

        [Fact]
        public void ReturnsSemanticVersionWhenMajorMinorPatchAndPreAndBuildIsFound()
        {
            var semanticVersion = SemanticVersion.Parse("1.2.3-pre+build");

            semanticVersion.Should().NotBeNull();
            semanticVersion.Major.Should().Be(1);
            semanticVersion.Minor.Should().Be(2);
            semanticVersion.Patch.Should().Be(3);
            semanticVersion.Pre.Should().Be("-pre");
            semanticVersion.Build.Should().Be("build");
        }
    }
}