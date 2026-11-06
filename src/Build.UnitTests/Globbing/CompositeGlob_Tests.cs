// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        [Theory]
        [InlineData("")]
        [InlineData("File.cs")]
        [InlineData("src/File.cs")]
        [InlineData("src/../File.cs")]
        [InlineData("../File.cs")]
        [InlineData("obj/File.cs")]
        [InlineData("src/File.vb")]
        [InlineData("src/FILE.CS")]
        [InlineData("src\\File.cs")]
        [InlineData("src/./nested/../File.cs")]
        [InlineData("src/\0File.cs")]
        public void NestedMatchingPreservesIndependentNormalization(string input)
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            string otherRoot = Path.Combine(root, "other");
            var glob = new CompositeGlob(
                MSBuildGlob.Parse(root, "**/*.txt"),
                new MSBuildGlobWithGaps(
                    new CompositeGlob(
                        MSBuildGlob.Parse(otherRoot, "**/*.vb"),
                        MSBuildGlob.Parse(root, "**/*.cs")),
                    MSBuildGlob.Parse(root, "**/obj/**"),
                    MSBuildGlob.Parse(otherRoot, "**/bin/**")),
                MSBuildGlob.Parse(root, "fallback/*"));

            Assert.Equal(MatchIndependently(glob, input), glob.IsMatch(input));

            if (input.IndexOf('\0') < 0)
            {
                string absoluteInput = Path.Combine(root, input);
                Assert.Equal(MatchIndependently(glob, absoluteInput), glob.IsMatch(absoluteInput));
            }
        }

        [Fact]
        public void DifferentRootsDoNotShareNormalizedInput()
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            string otherRoot = Path.Combine(root, "other");
            var glob = new CompositeGlob(
                MSBuildGlob.Parse(root, "**/*.txt"),
                MSBuildGlob.Parse(otherRoot, Path.Combine(root, "*.cs")));

            Assert.False(glob.IsMatch("File.cs"));
            Assert.True(glob.IsMatch(Path.Combine(root, "File.cs")));
        }

        [Fact]
        public void MatchContextReusesOnlyTheCurrentRootsNormalization()
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            string otherRoot = Path.Combine(root, "other");
            var context = new GlobMatchContext("File.cs");
            string normalizedInput = context.GetNormalizedInput(root);

            Assert.Same(normalizedInput, context.GetNormalizedInput(root));
            Assert.Equal(Path.Combine(otherRoot, "File.cs"), context.GetNormalizedInput(otherRoot));
            string normalizedAgain = context.GetNormalizedInput(root);
            Assert.Equal(normalizedInput, normalizedAgain);

            context.IsMatch(new CallbackGlob(_ => false));
            Assert.NotSame(normalizedAgain, context.GetNormalizedInput(root));
        }

        [Fact]
        public void CustomMatchersReceiveOriginalInputAndShortCircuit()
        {
            const string Input = "src/../File.cs";
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            var inputs = new List<string>();
            var glob = new CompositeGlob(
                MSBuildGlob.Parse(root, "**/*.txt"),
                new CallbackGlob(input =>
                {
                    inputs.Add(input);
                    return false;
                }),
                new MSBuildGlobWithGaps(
                    MSBuildGlob.Parse(root, "**/*.cs"),
                    new CallbackGlob(input =>
                    {
                        inputs.Add(input);
                        return false;
                    })),
                new CallbackGlob(_ => throw new InvalidOperationException("Matching must short circuit.")));

            Assert.True(glob.IsMatch(Input));
            Assert.Equal(new[] { Input, Input }, inputs);
        }

        [Fact]
        public void ContextAwareMatchersShareNormalization()
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            MSBuildGlob match = MSBuildGlob.Parse(root, "**/*.cs");
            string normalizedInput = null;
            var glob = new CompositeGlob(
                new ContextAwareGlob(match.TestOnlyGlobRoot, input => normalizedInput = input),
                new MSBuildGlobWithGaps(
                    match,
                    new ContextAwareGlob(match.TestOnlyGlobRoot, input => Assert.Same(normalizedInput, input))));

            Assert.True(glob.IsMatch("File.cs"));
            Assert.Equal(Path.Combine(root, "File.cs"), normalizedInput);
        }

        [Fact]
        public void DerivedMatchersKeepTheirInheritedImplementation()
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            MSBuildGlob match = MSBuildGlob.Parse(root, "**/*.cs");

            Assert.True(new CompositeGlob(new DerivedCompositeGlob(match)).IsMatch("File.cs"));
            Assert.True(new CompositeGlob(new DerivedGlobWithGaps(match)).IsMatch("File.cs"));
        }

        [Fact]
        public void EmptyCompositeDoesNotValidateInput()
        {
            Assert.False(new CompositeGlob().IsMatch(null));
            Assert.True(new CompositeGlob(new CallbackGlob(input => input is null)).IsMatch(null));
            Assert.Throws<ArgumentNullException>(() =>
                new CompositeGlob(MSBuildGlob.Parse("*")).IsMatch(null));
        }

        [Fact]
        public void MatchingDoesNotShareStateAcrossCalls()
        {
            string root = Path.Combine(Path.GetTempPath(), "GlobRoot");
            var glob = new MSBuildGlobWithGaps(
                MSBuildGlob.Parse(root, "**/*.cs"),
                MSBuildGlob.Parse(root, "**/obj/**"));
            var composite = new CompositeGlob(glob, MSBuildGlob.Parse(root, "**/*.vb"));
            string[] inputs = { "src/File.cs", "obj/File.cs", "src/File.txt", "src/File.vb", "" };
            bool[] expected = inputs.Select(input => MatchIndependently(composite, input)).ToArray();

            Parallel.For(0, 1000, i =>
            {
                int index = i % inputs.Length;
                Assert.Equal(expected[index], composite.IsMatch(inputs[index]));
            });
        }

        private static bool MatchIndependently(IMSBuildGlob glob, string input)
        {
            return glob switch
            {
                CompositeGlob composite => composite.Globs.Any(child => MatchIndependently(child, input)),
                MSBuildGlobWithGaps gaps => MatchIndependently(gaps.MainGlob, input) && !MatchIndependently(gaps.Gaps, input),
                _ => glob.IsMatch(input)
            };
        }

        private sealed class CallbackGlob(Func<string, bool> callback) : IMSBuildGlob
        {
            public bool IsMatch(string input) => callback(input);
        }

        private sealed class ContextAwareGlob(string root, Action<string> callback) : IMSBuildGlob, IContextAwareGlob
        {
            public bool IsMatch(string input) => throw new InvalidOperationException("The context-aware implementation must be used.");

            bool IContextAwareGlob.IsMatch(ref GlobMatchContext context)
            {
                callback(context.GetNormalizedInput(root));
                return false;
            }
        }

        private sealed class DerivedCompositeGlob(IMSBuildGlob glob) : CompositeGlob(glob);

        private sealed class DerivedGlobWithGaps(IMSBuildGlob glob) : MSBuildGlobWithGaps(glob);
    }
}
