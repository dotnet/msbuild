// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToCompareFXVersions
    {
        [Theory]
        [InlineData("2.0.0", "1.0.0", 1)]
        [InlineData("1.1.0", "1.0.0", 1)]
        [InlineData("1.0.1", "1.0.0", 1)]
        [InlineData("1.0.0", "1.0.0-pre", 1)]
        [InlineData("1.0.0-pre+2", "1.0.0-pre+1", 0)]
        [InlineData("1.0.0", "2.0.0", -1)]
        [InlineData("1.0.0", "1.1.0", -1)]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("1.0.0-pre", "1.0.0", -1)]
        [InlineData("1.0.0-pre+1", "1.0.0-pre+2", 0)]
        [InlineData("1.2.3", "1.2.3", 0)]
        [InlineData("1.2.3-pre", "1.2.3-pre", 0)]
        [InlineData("1.2.3-pre+1", "1.2.3-pre+1", 0)]
        public void OneFXVersionIsBiggerThanTheOther(string s1, string s2, int expectedResult)
        {
            FXVersion fxVersion1;
            FXVersion fxVersion2;
            FXVersion.TryParse(s1, out fxVersion1).Should().BeTrue();
            FXVersion.TryParse(s2, out fxVersion2).Should().BeTrue();
            FXVersion.Compare(fxVersion1, fxVersion2).Should().Be(expectedResult);
        }

        struct TestCase
        {
            public TestCase(string str, bool same)
            {
                Str = str;
                Same = same;
            }

            public string Str;
            public bool Same;
        };

        [Fact]
        public void OrderingMatchesSemVer200Rules()
        {
            TestCase[] orderedCases = new TestCase[]
            {
                new TestCase( "1.0.0-0.3.7",                false ),
                new TestCase( "1.0.0-alpha",                false ),
                new TestCase( "1.0.0-alpha+001",            true  ),
                new TestCase( "1.0.0-alpha.1",              false ),
                new TestCase( "1.0.0-alpha.beta",           false ),
                new TestCase( "1.0.0-beta",                 false ),
                new TestCase( "1.0.0-beta+exp.sha.5114f85", true  ),
                new TestCase( "1.0.0-beta.2",               false ),
                new TestCase( "1.0.0-beta.11",              false ),
                new TestCase( "1.0.0-rc.1",                 false ),
                new TestCase( "1.0.0-x.7.z.92",             false ),
                new TestCase( "1.0.0",                      false ),
                new TestCase( "1.0.0+20130313144700",       true  ),
                new TestCase( "1.9.0-9",                    false ),
                new TestCase( "1.9.0-10",                   false ),
                new TestCase( "1.9.0-1A",                   false ),
                new TestCase( "1.9.0",                      false ),
                new TestCase( "1.10.0",                     false ),
                new TestCase( "1.11.0",                     false ),
                new TestCase( "2.0.0",                      false ),
                new TestCase( "2.1.0",                      false ),
                new TestCase( "2.1.1",                      false ),
                new TestCase( "4.6.0-preview.19064.1",      false ),
                new TestCase( "4.6.0-preview1-27018-01",    false ),
            };

            int isame = 0;

            for (int i = 0; i < orderedCases.Length; ++i)
            {
                if (orderedCases[i].Same) isame++;

                int jsame = 0;

                for (int j = 0; j < orderedCases.Length; ++j)
                {
                    if (orderedCases[j].Same) jsame++;

                    int expected = (i - isame) == (j - jsame) ? 0 : ((i - isame) > (j - jsame) ? 1 : -1);

                    OneFXVersionIsBiggerThanTheOther(orderedCases[i].Str, orderedCases[j].Str, expected);
                }
            }
        }
    }
}
