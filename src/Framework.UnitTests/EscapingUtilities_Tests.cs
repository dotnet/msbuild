// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public sealed class EscapingUtilities_Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("foo", "foo")]
    [InlineData("foo%", "foo%")]
    [InlineData("foo%3", "foo%3")]
    [InlineData("foo%20space", "foo space")]
    [InlineData("foo2%3B", "foo2;")]
    [InlineData("%3bfoo3", ";foo3")]
    [InlineData("%3b", ";")]
    [InlineData("%3b%3B;%3b%3B", ";;;;;")]
    [InlineData("%253B", "%3B")]
    [InlineData("===%ZZ%20%%%===", "===%ZZ %%%===")]
    [InlineData("hello%3B escaping%25 how%28 are%29 you%3f", "hello; escaping% how( are) you?")]
    [InlineData("%25*?*%25*", "%*?*%*")]
    [InlineData("%25%2a%3f%2a%25%2a", "%*?*%*")]
    [InlineData("%2aStar%2Acraft%20or %2aWar%2Acr%40ft%3f%3F", "*Star*craft or *War*cr@ft??")]
    public void Unescape(string value, string result)
        => EscapingUtilities.UnescapeAll(value).ShouldBe(result);

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  foo  ", "foo")]
    [InlineData("\tfoo\t", "foo")]
    [InlineData("  %3B  ", ";")]
    [InlineData("  %3b%3B  ", ";;")]
    [InlineData("\t%2a\t", "*")]
    [InlineData("  foo%3Bbar  ", "foo;bar")]
    [InlineData("  %3B", ";")]
    [InlineData("%3B  ", ";")]
    [InlineData("%20foo", " foo")]
    [InlineData("foo%20", "foo ")]
    [InlineData("  %ZZ  ", "%ZZ")]
    public void UnescapeWithTrim(string value, string result)
    => EscapingUtilities.UnescapeAll(value, trim: true).ShouldBe(result);

    [Theory]
    [InlineData("", "")]
    [InlineData("foo", "foo")]
    [InlineData("@", "%40")]
    [InlineData("$", "%24")]
    [InlineData("(", "%28")]
    [InlineData(")", "%29")]
    [InlineData(";", "%3b")]
    [InlineData("'", "%27")]
    [InlineData("*", "%2a")]
    [InlineData("?", "%3f")]
    [InlineData("#*?*#*", "#%2a%3f%2a#%2a")]
    [InlineData("%*?*%*", "%25%2a%3f%2a%25%2a")]
    public void Escape(string value, string result)
        => EscapingUtilities.Escape(value).ShouldBe(result);

    [Theory]
    [InlineData("*")]
    [InlineData("?")]
    [InlineData("#*?*#*")]
    public void UnescapeEscape(string value)
        => EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(value)).ShouldBe(value);

    [Theory]
    [InlineData("%2a")]
    [InlineData("%3f")]
    [InlineData("#%2a%3f%2a#%2a")]
    public void EscapeUnescape(string value)
        => EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(value)).ShouldBe(value);

    [Theory]
    [InlineData("", false)]
    [InlineData("NoStarOrQMark", false)]
    [InlineData("%", false)]
    [InlineData("%%", false)]
    [InlineData("%2", false)]
    [InlineData("%4", false)]
    [InlineData("%3A", false)]
    [InlineData("%2B", false)]
    [InlineData("%2a", true)]
    [InlineData("%2A", true)]
    [InlineData("%3F", true)]
    [InlineData("%3f", true)]
    [InlineData("%%3f", true)]
    [InlineData("%3%3f", true)]
    public void ContainsEscapedWildcards(string value, bool expectedResult)
        => EscapingUtilities.ContainsEscapedWildcards(value).ShouldBe(expectedResult);
}
