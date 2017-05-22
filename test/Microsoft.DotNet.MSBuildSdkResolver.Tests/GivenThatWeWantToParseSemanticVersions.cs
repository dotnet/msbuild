// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Microsoft.DotNet.MSBuildSdkResolver;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToParseFXVersions
    {
        [Fact]
        public void ReturnsNullWhenNoMajorSeparatorIsFound()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1", out fxVersion).Should().BeFalse();
        }

        [Fact]
        public void ReturnsNullWhenMajorPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("a.0.0", out fxVersion).Should().BeFalse();
        }

        [Fact]
        public void ReturnsNullWhenNoMinorSeparatorIsFound()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.0", out fxVersion).Should().BeFalse();
        }

        [Fact]
        public void ReturnsNullWhenMinorPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.a.0", out fxVersion).Should().BeFalse();
        }

        [Fact]
        public void ReturnsNullWhenPatchPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.0.a", out fxVersion).Should().BeFalse();
        }

        [Fact]
        public void ReturnsFXVersionWhenOnlyMajorMinorPatchIsFound()
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse("1.2.3", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
        }

        [Fact]
        public void ReturnsFXVersionWhenOnlyMajorMinorPatchAndPreIsFound()
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse("1.2.3-pre", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
            fxVersion.Pre.Should().Be("-pre");
        }

        [Fact]
        public void ReturnsFXVersionWhenMajorMinorPatchAndPreAndBuildIsFound()
        {
            FXVersion fxVersion;
            
            var result = FXVersion.TryParse("1.2.3-pre+build", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
            fxVersion.Pre.Should().Be("-pre");
            fxVersion.Build.Should().Be("build");
        }
    }
}