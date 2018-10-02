// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class XmlUtilities_Tests
    {
        // Various invalid names, with the first invalid character listed for each
        private string[,] _invalidNames = new string[,] {
            {"foo.bar", "."},
            {"1baz", "1"},
            {"+", "+"},
            {"a+", "+"},
            {"_-.", "."},
            {"AZaz09%", "%"},
            {" x", " "},
            {"A Zaz09%", " "},
            {"foo.", "."},
            {"\u03A3", "\u03A3"},       // sigma
            {"a1\u03A3", "\u03A3"}
        };

        /// <summary>
        /// Verify we get the right invalid char listed in the error string
        /// </summary>
        [Fact]
        public void InvalidNameErrorLocation()
        {
            for (int i = 0; i <= _invalidNames.GetUpperBound(0); i++)
            {
                InvalidNameErrorLocationHelper(_invalidNames[i, 0], _invalidNames[i, 1]);
            }
        }

        /// <summary>
        /// Helper for invalid name error location test 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="badChar"></param>
        private void InvalidNameErrorLocationHelper(string name, string badChar)
        {
            string expected = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("OM_NameInvalid", name, badChar);
            string actual = String.Empty;

            try
            {
                XmlUtilities.VerifyThrowArgumentValidElementName(name);
            }
            catch (ArgumentException ex)
            {
                actual = ex.Message;
            }

            Assert.Equal(expected, actual);
        }
    }
}
