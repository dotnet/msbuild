// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CodeDom;
using Shouldly;
using Xunit;

using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    public class StringExtensions_Tests
    {
        [Theory]
        [InlineData("a", "a", "x", StringComparison.CurrentCulture, "x")]
        [InlineData("ab", "ab", "x", StringComparison.CurrentCulture, "x")]
        [InlineData("ab", "ab", "xyz", StringComparison.CurrentCulture, "xyz")]

        [InlineData("ab12", "ab", "x", StringComparison.CurrentCulture, "x12")]
        [InlineData("ab12", "ab", "xyz", StringComparison.CurrentCulture, "xyz12")]
        [InlineData("a12", "a", "xyz", StringComparison.CurrentCulture, "xyz12")]

        [InlineData("12ab", "ab", "x", StringComparison.CurrentCulture, "12x")]
        [InlineData("12ab", "ab", "xyz", StringComparison.CurrentCulture, "12xyz")]
        [InlineData("12a", "a", "xyz", StringComparison.CurrentCulture, "12xyz")]

        [InlineData("12ab345ab56", "ab", "x", StringComparison.CurrentCulture, "12x345x56")]
        [InlineData("12ab345ab56", "ab", "xyz", StringComparison.CurrentCulture, "12xyz345xyz56")]

        [InlineData("ab", "ab", null, StringComparison.CurrentCulture, "")]

        [InlineData("ab12", "ab", null, StringComparison.CurrentCulture, "12")]
        [InlineData("a12", "a", null, StringComparison.CurrentCulture, "12")]

        [InlineData("12ab", "ab", null, StringComparison.CurrentCulture, "12")]

        [InlineData("12ab345ab56", "ab", null, StringComparison.CurrentCulture, "1234556")]
        [InlineData("12ab345ab56", "ab", "", StringComparison.CurrentCulture, "1234556")]

        [InlineData("aaa", "aa", "x", StringComparison.CurrentCulture, "xa")]
        [InlineData("aaa", "aa", "a", StringComparison.CurrentCulture, "aa")]

        [InlineData("a", "a", "a", StringComparison.CurrentCulture, "a")]
        [InlineData("ab", "ab", "ab", StringComparison.CurrentCulture, "ab")]

        [InlineData("abab", "ab", "", StringComparison.CurrentCulture, "")]
        [InlineData("abab", "ab", "x", StringComparison.CurrentCulture, "xx")]
        [InlineData("1abab2", "ab", "x", StringComparison.CurrentCulture, "1xx2")]

        [InlineData("12ab34", "ab", "abc", StringComparison.CurrentCulture, "12abc34")]

        [InlineData("", "ab", "abc", StringComparison.CurrentCulture, "")]

        [InlineData("abc", "x", "y", StringComparison.CurrentCulture, "abc")]
        [InlineData("", "x", "y", StringComparison.CurrentCulture, "")]

        [InlineData(@"$(MsBuildExtensionsPath)\build.props", "$(MSBuildExtensionsPath)", @"c:\build", StringComparison.OrdinalIgnoreCase, @"c:\build\build.props")]
        public void ReplaceWithStringComparerTest(string aString, string oldValue, string newValue, StringComparison stringComparison, string expectedReplace)
        {
            aString.Replace(oldValue, newValue, stringComparison).ShouldBe(expectedReplace);
        }

        [Theory]
        [InlineData(null, "ab", "x", StringComparison.CurrentCulture, typeof(ArgumentNullException))]
        [InlineData("ab", null, "x", StringComparison.CurrentCulture, typeof(ArgumentNullException))]
        [InlineData("ab", "", "x", StringComparison.CurrentCulture, typeof(ArgumentException))]
        public void ReplaceWithStringComparerExceptionCases(string aString, string oldValue, string newValue, StringComparison stringComparison, Type expectedException)
        {
            Should.Throw(() => StringExtensions.Replace(aString, oldValue, newValue, stringComparison), expectedException);
        }
    }
}
