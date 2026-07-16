// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class StringExtensions_Tests
    {
        [MSBuildTestMethod]
        [DataRow("a", "a", "x", StringComparison.CurrentCulture, "x")]
        [DataRow("ab", "ab", "x", StringComparison.CurrentCulture, "x")]
        [DataRow("ab", "ab", "xyz", StringComparison.CurrentCulture, "xyz")]

        [DataRow("ab12", "ab", "x", StringComparison.CurrentCulture, "x12")]
        [DataRow("ab12", "ab", "xyz", StringComparison.CurrentCulture, "xyz12")]
        [DataRow("a12", "a", "xyz", StringComparison.CurrentCulture, "xyz12")]

        [DataRow("12ab", "ab", "x", StringComparison.CurrentCulture, "12x")]
        [DataRow("12ab", "ab", "xyz", StringComparison.CurrentCulture, "12xyz")]
        [DataRow("12a", "a", "xyz", StringComparison.CurrentCulture, "12xyz")]

        [DataRow("12ab345ab56", "ab", "x", StringComparison.CurrentCulture, "12x345x56")]
        [DataRow("12ab345ab56", "ab", "xyz", StringComparison.CurrentCulture, "12xyz345xyz56")]

        [DataRow("ab", "ab", null, StringComparison.CurrentCulture, "")]

        [DataRow("ab12", "ab", null, StringComparison.CurrentCulture, "12")]
        [DataRow("a12", "a", null, StringComparison.CurrentCulture, "12")]

        [DataRow("12ab", "ab", null, StringComparison.CurrentCulture, "12")]

        [DataRow("12ab345ab56", "ab", null, StringComparison.CurrentCulture, "1234556")]
        [DataRow("12ab345ab56", "ab", "", StringComparison.CurrentCulture, "1234556")]

        [DataRow("aaa", "aa", "x", StringComparison.CurrentCulture, "xa")]
        [DataRow("aaa", "aa", "a", StringComparison.CurrentCulture, "aa")]

        [DataRow("a", "a", "a", StringComparison.CurrentCulture, "a")]
        [DataRow("ab", "ab", "ab", StringComparison.CurrentCulture, "ab")]

        [DataRow("abab", "ab", "", StringComparison.CurrentCulture, "")]
        [DataRow("abab", "ab", "x", StringComparison.CurrentCulture, "xx")]
        [DataRow("1abab2", "ab", "x", StringComparison.CurrentCulture, "1xx2")]

        [DataRow("12ab34", "ab", "abc", StringComparison.CurrentCulture, "12abc34")]

        [DataRow("", "ab", "abc", StringComparison.CurrentCulture, "")]

        [DataRow("abc", "x", "y", StringComparison.CurrentCulture, "abc")]
        [DataRow("", "x", "y", StringComparison.CurrentCulture, "")]

        [DataRow(@"$(MsBuildExtensionsPath)\build.props", "$(MSBuildExtensionsPath)", @"c:\build", StringComparison.OrdinalIgnoreCase, @"c:\build\build.props")]
        public void ReplaceWithStringComparerTest(string aString, string oldValue, string newValue, StringComparison stringComparison, string expectedReplace)
        {
            aString.Replace(oldValue, newValue, stringComparison).ShouldBe(expectedReplace);
        }

        [MSBuildTestMethod]
        [DataRow(null, "ab", "x", StringComparison.CurrentCulture, typeof(ArgumentNullException))]
        [DataRow("ab", null, "x", StringComparison.CurrentCulture, typeof(ArgumentNullException))]
        [DataRow("ab", "", "x", StringComparison.CurrentCulture, typeof(ArgumentException))]
        public void ReplaceWithStringComparerExceptionCases(string aString, string oldValue, string newValue, StringComparison stringComparison, Type expectedException)
        {
            Should.Throw(() => Build.Shared.StringExtensions.Replace(aString, oldValue, newValue, stringComparison), expectedException);
        }
    }
}
