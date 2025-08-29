// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class EscapingUtilities_Tests
    {
        /// <summary>
        /// </summary>
        [Fact]
        public void Unescape()
        {
            Assert.Equal("", EscapingUtilities.UnescapeAll(""));
            Assert.Equal("foo", EscapingUtilities.UnescapeAll("foo"));
            Assert.Equal("foo space", EscapingUtilities.UnescapeAll("foo%20space"));
            Assert.Equal("foo2;", EscapingUtilities.UnescapeAll("foo2%3B"));
            Assert.Equal(";foo3", EscapingUtilities.UnescapeAll("%3bfoo3"));
            Assert.Equal(";", EscapingUtilities.UnescapeAll("%3b"));
            Assert.Equal(";;;;;", EscapingUtilities.UnescapeAll("%3b%3B;%3b%3B"));
            Assert.Equal("%3B", EscapingUtilities.UnescapeAll("%253B"));
            Assert.Equal("===%ZZ %%%===", EscapingUtilities.UnescapeAll("===%ZZ%20%%%==="));
            Assert.Equal("hello; escaping% how( are) you?", EscapingUtilities.UnescapeAll("hello%3B escaping%25 how%28 are%29 you%3f"));

            Assert.Equal("%*?*%*", EscapingUtilities.UnescapeAll("%25*?*%25*"));
            Assert.Equal("%*?*%*", EscapingUtilities.UnescapeAll("%25%2a%3f%2a%25%2a"));

            Assert.Equal("*Star*craft or *War*cr@ft??", EscapingUtilities.UnescapeAll("%2aStar%2Acraft%20or %2aWar%2Acr%40ft%3f%3F"));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void Escape()
        {
            Assert.Equal("%2a", EscapingUtilities.Escape("*"));
            Assert.Equal("%3f", EscapingUtilities.Escape("?"));
            Assert.Equal("#%2a%3f%2a#%2a", EscapingUtilities.Escape("#*?*#*"));
            Assert.Equal("%25%2a%3f%2a%25%2a", EscapingUtilities.Escape("%*?*%*"));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void UnescapeEscape()
        {
            string text = "*";
            Assert.Equal(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "?";
            Assert.Equal(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "#*?*#*";
            Assert.Equal(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void EscapeUnescape()
        {
            string text = "%2a";
            Assert.Equal(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "%3f";
            Assert.Equal(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "#%2a%3f%2a#%2a";
            Assert.Equal(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));
        }

        [Fact]
        public void ContainsEscapedWildcards()
        {
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("NoStarOrQMark"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%%"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%2"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%4"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%3A"));
            Assert.False(EscapingUtilities.ContainsEscapedWildcards("%2B"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%2a"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%2A"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%3F"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%3f"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%%3f"));
            Assert.True(EscapingUtilities.ContainsEscapedWildcards("%3%3f"));
        }
    }
}
