// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for <see cref="BuiltInMetadataExpander"/>, which expands references such as <c>%(Filename)</c> when
    /// an item is read after it crossed a process boundary.
    /// </summary>
    public class BuiltInMetadataExpander_Tests
    {
        private static readonly string s_sep = Path.DirectorySeparatorChar.ToString();
        private static readonly string s_itemSpec = $"folder{Path.DirectorySeparatorChar}hello.txt";

        private static string? Expand(string? value, string? recursiveDir = null, string? itemSpec = null)
        {
            ItemSpecModifiers.Cache cache = default;

            return BuiltInMetadataExpander.Expand(
                value,
                itemSpec ?? s_itemSpec,
                escapedDefiningProject: "project.proj",
                escapedRecursiveDir: recursiveDir,
                ref cache);
        }

        [Theory]
        [InlineData("%(Filename)", "hello")]
        [InlineData("%(Extension)", ".txt")]
        [InlineData("%(Identity)", @"folder\hello.txt")]
        [InlineData("a%(Filename)b", "ahellob")]
        [InlineData("%(Filename)%(Extension)", "hello.txt")]
        [InlineData("%(Filename)%(Filename)", "hellohello")]
        [InlineData("%(Filename)trailing", "hellotrailing")]
        [InlineData("leading%(Filename)", "leadinghello")]
        public void ExpandsBuiltInMetadata(string value, string expected)
            => Expand(value).ShouldBe(expected.Replace(@"\", s_sep));

        [Theory]
        [InlineData("%( Filename )", "hello")]
        [InlineData("%(  Filename  )", "hello")]
        [InlineData("%(FILENAME)", "hello")]
        [InlineData("%(filename)", "hello")]
        public void AcceptsWhitespaceAndAnyCasing(string value, string expected)
            => Expand(value).ShouldBe(expected);

        [Theory]
        // No reference at all.
        [InlineData("")]
        [InlineData("plain text")]
        [InlineData("100% done")]
        // Not a well formed reference.
        [InlineData("%(Filename")]
        [InlineData("a%(")]
        [InlineData("%()")]
        [InlineData("%(  )")]
        [InlineData("%((Filename)")]
        [InlineData("%(Fi lename)")]
        // A name that is not built-in metadata. Evaluation expands custom metadata, so a value that reaches
        // this point with one left in it is text.
        [InlineData("%(NotAModifier)")]
        public void LeavesEverythingElseAsItIs(string value)
            => Expand(value).ShouldBe(value);

        [Fact]
        public void ReturnsNullForNull()
            => Expand(null).ShouldBeNull();

        /// <summary>
        /// A value with nothing to expand must come back as the same instance, not a copy. Metadata is read on
        /// hot paths, and most values have no reference in them.
        /// </summary>
        [Fact]
        public void DoesNotAllocateWhenThereIsNothingToExpand()
        {
            string value = "no reference here";

            Expand(value).ShouldBeSameAs(value);
            Expand("%(NotAModifier)").ShouldBeSameAs("%(NotAModifier)");
        }

        /// <summary>
        /// RecursiveDir comes from the wildcard the item was expanded from, so it is supplied rather than derived.
        /// </summary>
        [Theory]
        [InlineData(@"sub1\sub2\", @"out\sub1\sub2\hello.txt")]
        [InlineData("", @"out\hello.txt")]
        [InlineData(null, @"out\hello.txt")]
        public void UsesTheSuppliedRecursiveDir(string? recursiveDir, string expected)
            => Expand(@"out\%(RecursiveDir)%(Filename)%(Extension)".Replace(@"\", s_sep), recursiveDir?.Replace(@"\", s_sep))
                .ShouldBe(expected.Replace(@"\", s_sep));

        /// <summary>
        /// The expander derives from the item spec it is given, so the same value gives a different result for a
        /// different item. This is why metadata is expanded on each read instead of one time.
        /// </summary>
        [Fact]
        public void DerivesFromTheGivenItemSpec()
        {
            Expand("%(Filename)", itemSpec: $"other{s_sep}renamed.md").ShouldBe("renamed");
            Expand("%(Extension)", itemSpec: $"other{s_sep}renamed.md").ShouldBe(".md");
        }

        /// <summary>
        /// A reference that is not the first thing in the value must still be found after an earlier reference
        /// failed to parse.
        /// </summary>
        [Theory]
        [InlineData("%(NotAModifier)%(Filename)", "%(NotAModifier)hello")]
        [InlineData("%(Filename)%(NotAModifier)", "hello%(NotAModifier)")]
        [InlineData("%(Fi lename)%(Filename)", "%(Fi lename)hello")]
        public void FindsLaterReferencesAfterOneThatDoesNotResolve(string value, string expected)
            => Expand(value).ShouldBe(expected);

        /// <summary>
        /// An unterminated reference must not hide a well formed one that follows it inside the same text.
        /// </summary>
        [Theory]
        [InlineData("%(foo%(Filename)", "%(foohello")]
        [InlineData("%(%(Filename)", "%(hello")]
        public void FindsAReferenceNestedInsideOneThatDoesNotResolve(string value, string expected)
            => Expand(value).ShouldBe(expected);

        /// <summary>
        /// The scan looks for '%' alone and tests the next character, because a single character search
        /// vectorizes. These cases pin that it still agrees with searching for "%(" directly.
        /// </summary>
        [Theory]
        [InlineData("", -1)]
        [InlineData("%", -1)]
        [InlineData("no marker here", -1)]
        [InlineData("100% done", -1)]
        [InlineData("50%", -1)]
        [InlineData("%(", 0)]
        [InlineData("a%(", 1)]
        [InlineData("%x%(", 2)]
        [InlineData("%%%(", 2)]
        [InlineData("%a%b%(c", 4)]
        [InlineData("trailing%", -1)]
        public void FindsTheMetadataMarker(string value, int expected)
            => BuiltInMetadataExpander.IndexOfMetadataMarker(value, 0).ShouldBe(expected);

        [Theory]
        [InlineData("%(Filename)%(Extension)", 11, 11)]
        [InlineData("%(Filename)plain", 11, -1)]
        [InlineData("abc", 3, -1)]
        public void FindsTheMetadataMarkerFromAStartIndex(string value, int startIndex, int expected)
            => BuiltInMetadataExpander.IndexOfMetadataMarker(value, startIndex).ShouldBe(expected);
    }
}
