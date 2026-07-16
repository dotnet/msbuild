// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Shouldly;

namespace Microsoft.Build.Framework.UnitTests;

[TestClass]

public sealed class EscapingUtilities_Tests
{
    [MSBuildTestMethod]
    [DataRow("", "")]
    [DataRow("foo", "foo")]
    [DataRow("foo%", "foo%")]
    [DataRow("foo%3", "foo%3")]
    [DataRow("foo%20space", "foo space")]
    [DataRow("foo2%3B", "foo2;")]
    [DataRow("%3bfoo3", ";foo3")]
    [DataRow("%3b", ";")]
    [DataRow("%3b%3B;%3b%3B", ";;;;;")]
    [DataRow("%253B", "%3B")]
    [DataRow("===%ZZ%20%%%===", "===%ZZ %%%===")]
    [DataRow("hello%3B escaping%25 how%28 are%29 you%3f", "hello; escaping% how( are) you?")]
    [DataRow("%25*?*%25*", "%*?*%*")]
    [DataRow("%25%2a%3f%2a%25%2a", "%*?*%*")]
    [DataRow("%2aStar%2Acraft%20or %2aWar%2Acr%40ft%3f%3F", "*Star*craft or *War*cr@ft??")]
    public void Unescape(string value, string result)
        => EscapingUtilities.UnescapeAll(value).ShouldBe(result);

    [MSBuildTestMethod]
    [DataRow("", "")]
    [DataRow("   ", "")]
    [DataRow("  foo  ", "foo")]
    [DataRow("\tfoo\t", "foo")]
    [DataRow("  %3B  ", ";")]
    [DataRow("  %3b%3B  ", ";;")]
    [DataRow("\t%2a\t", "*")]
    [DataRow("  foo%3Bbar  ", "foo;bar")]
    [DataRow("  %3B", ";")]
    [DataRow("%3B  ", ";")]
    [DataRow("%20foo", " foo")]
    [DataRow("foo%20", "foo ")]
    [DataRow("  %ZZ  ", "%ZZ")]
    public void UnescapeWithTrim(string value, string result)
    => EscapingUtilities.UnescapeAll(value, trim: true).ShouldBe(result);

    [MSBuildTestMethod]
    [DataRow("", "")]
    [DataRow("foo", "foo")]
    [DataRow("@", "%40")]
    [DataRow("$", "%24")]
    [DataRow("(", "%28")]
    [DataRow(")", "%29")]
    [DataRow(";", "%3b")]
    [DataRow("'", "%27")]
    [DataRow("*", "%2a")]
    [DataRow("?", "%3f")]
    [DataRow("#*?*#*", "#%2a%3f%2a#%2a")]
    [DataRow("%*?*%*", "%25%2a%3f%2a%25%2a")]
    public void Escape(string value, string result)
        => EscapingUtilities.Escape(value).ShouldBe(result);

    [MSBuildTestMethod]
    [DataRow("*")]
    [DataRow("?")]
    [DataRow("#*?*#*")]
    public void UnescapeEscape(string value)
        => EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(value)).ShouldBe(value);

    [MSBuildTestMethod]
    [DataRow("%2a")]
    [DataRow("%3f")]
    [DataRow("#%2a%3f%2a#%2a")]
    public void EscapeUnescape(string value)
        => EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(value)).ShouldBe(value);

    [MSBuildTestMethod]
    [DataRow("", false)]
    [DataRow("NoStarOrQMark", false)]
    [DataRow("%", false)]
    [DataRow("%%", false)]
    [DataRow("%2", false)]
    [DataRow("%4", false)]
    [DataRow("%3A", false)]
    [DataRow("%2B", false)]
    [DataRow("%2a", true)]
    [DataRow("%2A", true)]
    [DataRow("%3F", true)]
    [DataRow("%3f", true)]
    [DataRow("%%3f", true)]
    [DataRow("%3%3f", true)]
    public void ContainsEscapedWildcards(string value, bool expectedResult)
        => EscapingUtilities.ContainsEscapedWildcards(value).ShouldBe(expectedResult);
}
