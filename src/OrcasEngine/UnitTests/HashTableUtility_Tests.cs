// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class HashTableUtilityTests
    {
        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: HashTableUtility was not a separate class and
        /// there was no way to hit this case through BatchingEngine directly because it never 
        /// calls Compare() with unequal numbers of items.
        /// 
        /// This test ensures that hashtable     with unequal numbers of items are considered not 
        /// equivalent.
        /// </summary>
        [Test]
        public void Regress_Mutation_HashtablesWithDifferentCountsAreNotEquivalent()
        {
            Dictionary<string, string> h1 = new Dictionary<string, string>();
            h1["a"] = "x";                    // <---------- Must be the same in both hashtables.
            Dictionary<string, string> h2 = new Dictionary<string, string>();
            h2["a"] = "x";                    // <---------- Must be the same in both hashtables.
            h2["b"] = "y";

            Assertion.Assert(HashTableUtility.Compare(h1, h2) < 0);
            Assertion.Assert(HashTableUtility.Compare(h2, h1) > 0);
        }

        [Test]
        public void HashtableComparisons()
        {
            Dictionary<string, string> h1 = new Dictionary<string, string>();
            Dictionary<string, string> h2 = new Dictionary<string, string>();
            Assertion.Assert(HashTableUtility.Compare(h1, h2) == 0);

            h1["a"] = "x";
            h2["a"] = "x";
            Assertion.Assert(HashTableUtility.Compare(h1, h2) == 0);

            h1["b"] = "y";
            h1["c"] = "z";
            h2["b"] = "y";
            h2["c"] = "z";
            Assertion.Assert(HashTableUtility.Compare(h1, h2) == 0);

            h1["b"] = "j";
            Assertion.Assert(HashTableUtility.Compare(h1, h2) < 0);

            h2["b"] = "j";
            h2["c"] = "k";
            Assertion.Assert(HashTableUtility.Compare(h1, h2) > 0);

            h1["a"] = null;
            h1["c"] = "k";
            Assertion.Assert(HashTableUtility.Compare(h1, h2) < 0);

            h2["a"] = null;
            Assertion.Assert(HashTableUtility.Compare(h1, h2) == 0);
        }
    }
}
