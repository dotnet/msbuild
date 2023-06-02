// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Globbing;
using Microsoft.Build.Globbing.Extensions;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    public class CompositeGlobTests
    {
        public static IEnumerable<object[]> CompositeMatchingTestData
        {
            get
            {
                yield return new object[]
                {
                    new CompositeGlob(MSBuildGlob.Parse("a*")),
                    "abc", // string to match
                    true // should match
                };

                yield return new object[]
                {
                    new CompositeGlob(MSBuildGlob.Parse("a*")),
                    "bcd", // string to match
                    false // should match
                };

                yield return new object[]
                {
                    new CompositeGlob(
                        MSBuildGlob.Parse("a*"),
                        MSBuildGlob.Parse("b*"),
                        MSBuildGlob.Parse("c*")),
                    "bcd",
                    true
                };

                yield return new object[]
                {
                    new CompositeGlob(
                        MSBuildGlob.Parse("*"),
                        MSBuildGlob.Parse("*"),
                        MSBuildGlob.Parse("*")),
                    "bcd",
                    true
                };

                yield return new object[]
                {
                    new CompositeGlob(
                        MSBuildGlob.Parse("a*"),
                        MSBuildGlob.Parse("b*"),
                        MSBuildGlob.Parse("c*")),
                    "def",
                    false
                };

                yield return new object[]
                {
                    new CompositeGlob(
                        MSBuildGlob.Parse("a*"),
                        new CompositeGlob(
                            MSBuildGlob.Parse("b*"),
                            MSBuildGlob.Parse("c*"),
                            MSBuildGlob.Parse("d*")),
                        MSBuildGlob.Parse("e*")),
                    "cde",
                    true
                };

                yield return new object[]
                {
                    new CompositeGlob(
                        MSBuildGlob.Parse("a*"),
                        new CompositeGlob(
                            MSBuildGlob.Parse("b*"),
                            MSBuildGlob.Parse("c*"),
                            MSBuildGlob.Parse("d*")),
                        MSBuildGlob.Parse("e*")),
                    "fgh",
                    false
                };
            }
        }

        [Theory]
        [MemberData(nameof(CompositeMatchingTestData))]
        public void CompositeMatching(CompositeGlob compositeGlob, string stringToMatch, bool shouldMatch)
        {
            if (shouldMatch)
            {
                Assert.True(compositeGlob.IsMatch(stringToMatch));
            }
            else
            {
                Assert.False(compositeGlob.IsMatch(stringToMatch));
            }
        }

        [Fact]
        public void MSBuildGlobVisitorShouldFindAllLeaves()
        {
            var g1 = MSBuildGlob.Parse("1*");
            var g2 = MSBuildGlob.Parse("2*");
            var g3 = MSBuildGlob.Parse("3*");
            var g4 = MSBuildGlob.Parse("4*");

            var expectedCollectedGlobs = new[]
            {
                g1,
                g2,
                g3,
                g4
            };

            var composite = new CompositeGlob(
                g1,
                g2,
                new CompositeGlob(
                    new MSBuildGlobWithGaps(g3, MSBuildGlob.Parse("x*")),
                    new CompositeGlob(
                        g4)));

            var leafGlobs = composite.GetParsedGlobs().ToArray();

            Assert.Equal(4, leafGlobs.Length);

            foreach (var expectedGlob in expectedCollectedGlobs)
            {
                Assert.Contains(expectedGlob, leafGlobs);
            }
        }

        [Fact]
        public void CreateShouldHandleZeroChildren()
        {
            IMSBuildGlob composite = CompositeGlob.Create(Enumerable.Empty<IMSBuildGlob>());

            Assert.False(composite.IsMatch(""));
        }

        [Fact]
        public void CreateShouldReturnSingleChildUnchanged()
        {
            var glob = MSBuildGlob.Parse("");

            IMSBuildGlob composite = CompositeGlob.Create(new[] { glob });

            Assert.Same(glob, composite);
        }

        [Fact]
        public void CreateShouldReturnNewCompositeWhenMultipleProvided()
        {
            var glob1 = MSBuildGlob.Parse("");
            var glob2 = MSBuildGlob.Parse("");

            IMSBuildGlob result = CompositeGlob.Create(new[] { glob1, glob2 });

            var composite = Assert.IsType<CompositeGlob>(result);
            Assert.Same(glob1, composite.Globs.First());
            Assert.Same(glob2, composite.Globs.Skip(1).First());
            Assert.Equal(2, composite.Globs.Count());
        }
    }
}
