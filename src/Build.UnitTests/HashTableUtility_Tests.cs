// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Microsoft.Build.Collections;


#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
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
        [MSBuildTestMethod]
        public void Regress_Mutation_HashtablesWithDifferentCountsAreNotEquivalent()
        {
            Dictionary<string, string> h1 = new Dictionary<string, string>();
            h1["a"] = "x";                    // <---------- Must be the same in both hashtables.
            Dictionary<string, string> h2 = new Dictionary<string, string>();
            h2["a"] = "x";                    // <---------- Must be the same in both hashtables.
            h2["b"] = "y";

            Assert.IsTrue(HashTableUtility.Compare(h1, h2) < 0);
            Assert.IsTrue(HashTableUtility.Compare(h2, h1) > 0);
        }

        [MSBuildTestMethod]
        public void HashtableComparisons()
        {
            Dictionary<string, string> h1 = new Dictionary<string, string>();
            Dictionary<string, string> h2 = new Dictionary<string, string>();
            Assert.AreEqual(0, HashTableUtility.Compare(h1, h2));

            h1["a"] = "x";
            h2["a"] = "x";
            Assert.AreEqual(0, HashTableUtility.Compare(h1, h2));

            h1["b"] = "y";
            h1["c"] = "z";
            h2["b"] = "y";
            h2["c"] = "z";
            Assert.AreEqual(0, HashTableUtility.Compare(h1, h2));

            h1["b"] = "j";
            Assert.IsTrue(HashTableUtility.Compare(h1, h2) < 0);

            h2["b"] = "j";
            h2["c"] = "k";
            Assert.IsTrue(HashTableUtility.Compare(h1, h2) > 0);

            h1["a"] = null;
            h1["c"] = "k";
            Assert.IsTrue(HashTableUtility.Compare(h1, h2) < 0);

            h2["a"] = null;
            Assert.AreEqual(0, HashTableUtility.Compare(h1, h2));
        }
    }
}
