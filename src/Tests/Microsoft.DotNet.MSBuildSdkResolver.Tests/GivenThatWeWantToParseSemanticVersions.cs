// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToParseFXVersions
    {
        [Theory]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("1.1")]
        [InlineData("A.1.1")]
        [InlineData("1.A.1")]
        [InlineData("1.1.A")]
        [InlineData("1A.1.1")]
        [InlineData("1.1A.1")]
        [InlineData("1.1.1A")]
        [InlineData("1.1.1-")]
        [InlineData("1.1.1-.")]
        [InlineData("1.1.1-A.")]
        [InlineData("1.1.1-A.B.")]
        [InlineData("1.1.1-.+id")]
        [InlineData("1.1.1-A.+id")]
        [InlineData("1.1.1-A.B.+id")]
        [InlineData("1.1.1-A.B+id.")]
        [InlineData("01.1.1")]
        [InlineData("1.01.1")]
        [InlineData("1.1.01")]
        [InlineData("1.1.1-01.B")]
        [InlineData("1.1.1-A.01")]
        [InlineData("00.1.1")]
        [InlineData("1.00.1")]
        [InlineData("1.1.00")]
        [InlineData("1.1.1-00.B")]
        [InlineData("1.1.1-A.00")]
        [InlineData("1.1.1+")]
        [InlineData("1.1.1-A+")]
        [InlineData("1.1.1-A*B")]
        [InlineData("1.1.1-A/B")]
        [InlineData("1.1.1-A:B")]
        [InlineData("1.1.1-A^B")]
        [InlineData("1.1.1-A|B")]
        public void ReturnsFalseGivenInvalidVersion(string s1)
        {
            FXVersion fxVersion;
            FXVersion.TryParse(s1, out fxVersion).Should().BeFalse();
        }

        [Theory]
        [InlineData("1.0.0-0.3.7",                1, 0, 0,  "-0.3.7",             "")]
        [InlineData("1.0.0-alpha",                1, 0, 0,  "-alpha",             "")]
        [InlineData("1.0.0-alpha+001",            1, 0, 0,  "-alpha",             "+001")]
        [InlineData("1.0.0-alpha.1",              1, 0, 0,  "-alpha.1",           "")]
        [InlineData("1.0.0-alpha.beta",           1, 0, 0,  "-alpha.beta",        "")]
        [InlineData("1.0.0-beta",                 1, 0, 0,  "-beta",              "")]
        [InlineData("1.0.0-beta+exp.sha.5114f85", 1, 0, 0,  "-beta",              "+exp.sha.5114f85")]
        [InlineData("1.0.0-beta.2",               1, 0, 0,  "-beta.2",            "")]
        [InlineData("1.0.0-beta.11",              1, 0, 0,  "-beta.11",           "")]
        [InlineData("1.0.0-rc.1",                 1, 0, 0,  "-rc.1",              "")]
        [InlineData("1.0.0-x.7.z.92",             1, 0, 0,  "-x.7.z.92",          "")]
        [InlineData("1.0.0",                      1, 0, 0,  "",                   "")]
        [InlineData("1.0.0+20130313144700",       1, 0, 0,  "",                   "+20130313144700")]
        [InlineData("1.9.0-9",                    1, 9, 0,  "-9",                 "")]
        [InlineData("1.9.0-10",                   1, 9, 0,  "-10",                "")]
        [InlineData("1.9.0-1A",                   1, 9, 0,  "-1A",                "")]
        [InlineData("1.9.0",                      1, 9, 0,  "",                   "")]
        [InlineData("1.10.0",                     1, 10, 0, "",                   "")]
        [InlineData("1.11.0",                     1, 11, 0, "",                   "")]
        [InlineData("2.0.0",                      2, 0, 0,  "",                   "")]
        [InlineData("2.1.0",                      2, 1, 0,  "",                   "")]
        [InlineData("2.1.1",                      2, 1, 1,  "",                   "")]
        [InlineData("4.6.0-preview.19064.1",      4, 6, 0,  "-preview.19064.1",   "")]
        [InlineData("4.6.0-preview1-27018-01",    4, 6, 0,  "-preview1-27018-01", "")]
        public void ReturnsCorrectFXVersion(string s1, int major, int minor, int patch, string pre, string build)
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse(s1, out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(major);
            fxVersion.Minor.Should().Be(minor);
            fxVersion.Patch.Should().Be(patch);
            fxVersion.Pre.Should().Be(pre);
            fxVersion.Build.Should().Be(build);
        }

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
            fxVersion.Build.Should().Be("+build");
        }

    }
}
