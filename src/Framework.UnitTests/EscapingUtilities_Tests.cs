// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
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
    [InlineData("prefixfoosuffix", 6, 3, "foo")]
    [InlineData("prefix%20suffix", 6, 3, " ")]
    [InlineData("prefixfoo%20suffix", 6, 6, "foo ")]
    [InlineData("prefix%ZZsuffix", 6, 3, "%ZZ")]
    [InlineData("prefixfoo%20suffix", 6, 4, "foo%")]
    [InlineData("prefix%20suffix", 7, 2, "20")]
    public void UnescapeRange(string value, int startIndex, int length, string expected)
        => EscapingUtilities.UnescapeAll(value, startIndex, length).ShouldBe(expected);

    [Fact]
    public void UnescapeFullRangeWithoutEscapesReturnsOriginalString()
    {
        string value = new('x', 3);

        EscapingUtilities.UnescapeAll(value, 0, value.Length).ShouldBeSameAs(value);
    }

    [Fact]
    public void UnescapeEmptyRanges()
    {
        string value = string.Empty;

        EscapingUtilities.UnescapeAll(value, 0, 0).ShouldBeSameAs(value);
        EscapingUtilities.UnescapeAll("value", 2, 0).ShouldBe(string.Empty);
        EscapingUtilities.UnescapeAll(null, 0, 0).ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 0, "startIndex")]
    [InlineData(4, 0, "startIndex")]
    [InlineData(0, -1, "length")]
    [InlineData(2, 2, "length")]
    public void UnescapeRangeRejectsInvalidBounds(int startIndex, int length, string parameterName)
    {
        ArgumentOutOfRangeException exception =
            Should.Throw<ArgumentOutOfRangeException>(() => EscapingUtilities.UnescapeAll("abc", startIndex, length));

        exception.ParamName.ShouldBe(parameterName);
    }

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
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("plain", 3)]
    [InlineData("a;b", 0)]
    [InlineData("a;b", 1)]
    [InlineData("a;b", 2)]
    [InlineData("a;b", 3)]
    [InlineData("%*?@$();'", 1)]
    [InlineData("%*?@$();'", 2)]
    [InlineData("%*?@$();'", 3)]
    public void AppendEscapedMatchesRepeatedEscaping(string? value, int escapeCount)
    {
        string? expected = value;
        for (int i = 0; i < escapeCount; i++)
        {
            expected = EscapingUtilities.Escape(expected);
        }

        ValueStringBuilder builder = new(initialCapacity: 16);
        try
        {
            builder.Append("prefix:");
            builder.AppendEscaped(value, escapeCount);
            builder.Append(":suffix");

            builder.ToString().ShouldBe($"prefix:{expected}:suffix");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Theory]
    [InlineData(';', 0, ";")]
    [InlineData(';', 1, "%3b")]
    [InlineData(';', 2, "%253b")]
    [InlineData(';', 3, "%25253b")]
    [InlineData('%', 2, "%2525")]
    [InlineData('a', 0, "a")]
    [InlineData('a', 1, "a")]
    [InlineData('a', 3, "a")]
    public void AppendEscapedCharacter(char value, int escapeCount, string expected)
    {
        ValueStringBuilder builder = new(initialCapacity: 16);
        try
        {
            builder.Append("prefix:");
            builder.AppendEscaped(value, escapeCount);
            builder.Append(":suffix");

            builder.ToString().ShouldBe($"prefix:{expected}:suffix");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AppendEscapedRejectsNegativeEscapeCount()
    {
        Should.Throw<ArgumentOutOfRangeException>(AppendStringWithNegativeEscapeCount)
            .ParamName.ShouldBe("escapeCount");
        Should.Throw<ArgumentOutOfRangeException>(AppendCharacterWithNegativeEscapeCount)
            .ParamName.ShouldBe("escapeCount");

        static void AppendStringWithNegativeEscapeCount()
        {
            ValueStringBuilder builder = new(initialCapacity: 16);
            try
            {
                builder.AppendEscaped("value", -1);
            }
            finally
            {
                builder.Dispose();
            }
        }

        static void AppendCharacterWithNegativeEscapeCount()
        {
            ValueStringBuilder builder = new(initialCapacity: 16);
            try
            {
                builder.AppendEscaped(';', -1);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }

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
