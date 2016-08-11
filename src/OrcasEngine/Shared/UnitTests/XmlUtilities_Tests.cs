// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class XmlUtilities_Tests
    {
        // Various invalid names, with the first invalid character listed for each
        private string[,] invalidNames = new string[,] {
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
        [Test]
        public void InvalidNameErrorLocation()
        {
            for (int i = 0; i <= invalidNames.GetUpperBound(0); i++)
            {
                InvalidNameErrorLocationHelper(invalidNames[i,0], invalidNames[i,1]);
            }
        }

        /// <summary>
        /// Helper for invalid name error location test 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="badChar"></param>
        private void InvalidNameErrorLocationHelper(string name, string badChar)
        {
            string expected = ResourceUtilities.FormatResourceString("NameInvalid", name, badChar);
            string actual = String.Empty;

            try
            {
                XmlUtilities.VerifyThrowValidElementName(name);
            }
            catch (ArgumentException ex)
            {
                actual = ex.Message;
            }

            Assertion.AssertEquals(expected, actual);
        }
    }
}
