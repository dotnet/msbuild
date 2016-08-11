// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#region Using directives

using System;
using NUnit.Framework;
using Microsoft.Build.BuildEngine.Shared;

#endregion

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    sealed public class EscapingUtilities_Tests
    {
        /// <summary>
        /// </summary>
        /// <owner>RGoel,LukaszG</owner>
        [Test]
        public void Unescape()
        {
            Assertion.AssertEquals("", EscapingUtilities.UnescapeAll(""));
            Assertion.AssertEquals("foo", EscapingUtilities.UnescapeAll("foo"));
            Assertion.AssertEquals("foo bar", EscapingUtilities.UnescapeAll("foo%20bar"));
            Assertion.AssertEquals("foobar;", EscapingUtilities.UnescapeAll("foobar%3B"));
            Assertion.AssertEquals(";foobar", EscapingUtilities.UnescapeAll("%3bfoobar"));
            Assertion.AssertEquals(";", EscapingUtilities.UnescapeAll("%3b"));
            Assertion.AssertEquals(";;;;;", EscapingUtilities.UnescapeAll("%3b%3B;%3b%3B"));
            Assertion.AssertEquals("%3B", EscapingUtilities.UnescapeAll("%253B"));
            Assertion.AssertEquals("===%ZZ %%%===", EscapingUtilities.UnescapeAll("===%ZZ%20%%%==="));
            Assertion.AssertEquals("hello; escaping% how( are) you?", EscapingUtilities.UnescapeAll("hello%3B escaping%25 how%28 are%29 you%3f"));

            Assertion.AssertEquals("%*?*%*", EscapingUtilities.UnescapeAll("%25*?*%25*"));
            Assertion.AssertEquals("%*?*%*", EscapingUtilities.UnescapeAll("%25%2a%3f%2a%25%2a"));

            Assertion.AssertEquals("*Star*craft or *War*cr@ft??", EscapingUtilities.UnescapeAll("%2aStar%2Acraft%20or %2aWar%2Acr%40ft%3f%3F"));
        }

        /// <summary>
        /// </summary>
        /// <owner>KieranMo</owner>
        [Test]
        public void Escape()
        {
            Assertion.AssertEquals("%2a", EscapingUtilities.Escape("*"));
            Assertion.AssertEquals("%3f", EscapingUtilities.Escape("?"));
            Assertion.AssertEquals("#%2a%3f%2a#%2a", EscapingUtilities.Escape("#*?*#*"));
            Assertion.AssertEquals("%25%2a%3f%2a%25%2a", EscapingUtilities.Escape("%*?*%*"));
        }

        /// <summary>
        /// </summary>
        /// <owner>KieranMo</owner>
        [Test]
        public void UnescapeEscape()
        {
            string text;

            text = "*";
            Assertion.AssertEquals(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "?";
            Assertion.AssertEquals(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "#*?*#*";
            Assertion.AssertEquals(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));
        }

        /// <summary>
        /// </summary>
        /// <owner>KieranMo</owner>
        [Test]
        public void EscapeUnescape()
        {
            string text;

            text = "%2a";
            Assertion.AssertEquals(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "%3f";
            Assertion.AssertEquals(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "#%2a%3f%2a#%2a";
            Assertion.AssertEquals(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));
        }

        [Test]
        public void ContainsEscapedWildcards()
        {
            Assertion.Assert(!EscapingUtilities.ContainsEscapedWildcards("NoStarOrQMark"));
            Assertion.Assert(!EscapingUtilities.ContainsEscapedWildcards("%4"));
            Assertion.Assert(!EscapingUtilities.ContainsEscapedWildcards("%3B"));
            Assertion.Assert(!EscapingUtilities.ContainsEscapedWildcards("%2B"));
            Assertion.Assert(EscapingUtilities.ContainsEscapedWildcards("%2a"));
            Assertion.Assert(EscapingUtilities.ContainsEscapedWildcards("%2A"));
            Assertion.Assert(EscapingUtilities.ContainsEscapedWildcards("%3F"));
            Assertion.Assert(EscapingUtilities.ContainsEscapedWildcards("%3f"));
        }

    }
}
