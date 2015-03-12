// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#region Using directives
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Shared;


#endregion
namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class EscapingUtilities_Tests
    {
        /// <summary>
        /// </summary>
        [TestMethod]
        public void Unescape()
        {
            Assert.AreEqual("", EscapingUtilities.UnescapeAll(""));
            Assert.AreEqual("foo", EscapingUtilities.UnescapeAll("foo"));
            Assert.AreEqual("foo space", EscapingUtilities.UnescapeAll("foo%20space"));
            Assert.AreEqual("foo2;", EscapingUtilities.UnescapeAll("foo2%3B"));
            Assert.AreEqual(";foo3", EscapingUtilities.UnescapeAll("%3bfoo3"));
            Assert.AreEqual(";", EscapingUtilities.UnescapeAll("%3b"));
            Assert.AreEqual(";;;;;", EscapingUtilities.UnescapeAll("%3b%3B;%3b%3B"));
            Assert.AreEqual("%3B", EscapingUtilities.UnescapeAll("%253B"));
            Assert.AreEqual("===%ZZ %%%===", EscapingUtilities.UnescapeAll("===%ZZ%20%%%==="));
            Assert.AreEqual("hello; escaping% how( are) you?", EscapingUtilities.UnescapeAll("hello%3B escaping%25 how%28 are%29 you%3f"));

            Assert.AreEqual("%*?*%*", EscapingUtilities.UnescapeAll("%25*?*%25*"));
            Assert.AreEqual("%*?*%*", EscapingUtilities.UnescapeAll("%25%2a%3f%2a%25%2a"));

            Assert.AreEqual("*Star*craft or *War*cr@ft??", EscapingUtilities.UnescapeAll("%2aStar%2Acraft%20or %2aWar%2Acr%40ft%3f%3F"));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void Escape()
        {
            Assert.AreEqual("%2a", EscapingUtilities.Escape("*"));
            Assert.AreEqual("%3f", EscapingUtilities.Escape("?"));
            Assert.AreEqual("#%2a%3f%2a#%2a", EscapingUtilities.Escape("#*?*#*"));
            Assert.AreEqual("%25%2a%3f%2a%25%2a", EscapingUtilities.Escape("%*?*%*"));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void UnescapeEscape()
        {
            string text;

            text = "*";
            Assert.AreEqual(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "?";
            Assert.AreEqual(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));

            text = "#*?*#*";
            Assert.AreEqual(text, EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(text)));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void EscapeUnescape()
        {
            string text;

            text = "%2a";
            Assert.AreEqual(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "%3f";
            Assert.AreEqual(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));

            text = "#%2a%3f%2a#%2a";
            Assert.AreEqual(text, EscapingUtilities.Escape(EscapingUtilities.UnescapeAll(text)));
        }

        [TestMethod]
        public void ContainsEscapedWildcards()
        {
            Assert.IsFalse(EscapingUtilities.ContainsEscapedWildcards("NoStarOrQMark"));
            Assert.IsFalse(EscapingUtilities.ContainsEscapedWildcards("%4"));
            Assert.IsFalse(EscapingUtilities.ContainsEscapedWildcards("%3B"));
            Assert.IsFalse(EscapingUtilities.ContainsEscapedWildcards("%2B"));
            Assert.IsTrue(EscapingUtilities.ContainsEscapedWildcards("%2a"));
            Assert.IsTrue(EscapingUtilities.ContainsEscapedWildcards("%2A"));
            Assert.IsTrue(EscapingUtilities.ContainsEscapedWildcards("%3F"));
            Assert.IsTrue(EscapingUtilities.ContainsEscapedWildcards("%3f"));
        }
    }
}
