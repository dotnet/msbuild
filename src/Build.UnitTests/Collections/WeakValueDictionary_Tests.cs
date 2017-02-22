//-----------------------------------------------------------------------
// <copyright file="WeakValueDictionary_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the weak value dictionary class</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the weak value dictionary class
    /// </summary>
    [TestFixture]
    public class WeakValueDictionaryTests
    {
        /// <summary>
        /// Magic number size of strings to allocate for GC tests.
        /// </summary>
        private const int BigMemoryFootprintTest = 1 * 1024 * 1024;

        /// <summary>
        /// Find with the same key inserted using the indexer
        /// </summary>
        [Test]
        public void Indexer_ReferenceFound()
        {
            string k1 = "key";
            string v1 = "value";

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            string v2 = dictionary[k1];

            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));
            Assertion.AssertEquals(true, dictionary.Contains(k1));
        }

        /// <summary>
        /// Find where the value is truly null
        /// </summary>
        [Test]
        public void Indexer_NullValue_ReferenceFound()
        {
            string k1 = "key";
            string v1 = null;

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            string v2 = dictionary[k1];

            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));
            Assertion.AssertEquals(true, dictionary.Contains(k1));

            // Should not scavenge values that are null, rather than collected
            dictionary.Scavenge();
            Assertion.AssertEquals(1, dictionary.Count);
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [Test]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Indexer_NotFound()
        {
            var dictionary = new WeakValueDictionary<string, string>();
            string value = dictionary["x"];
        }

        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [Test]
        public void TryGetValue_ReferenceFound()
        {
            string k1 = "key";
            string v1 = "value";

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            string v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assertion.AssertEquals(true, result);
            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find true null value
        /// </summary>
        [Test]
        public void TryGetNullValue_ReferenceFound()
        {
            string k1 = "key";
            string v1 = null;

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            string v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assertion.AssertEquals(true, result);
            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));

            // Should not scavenge values that are null, rather than collected
            dictionary.Scavenge();
            Assertion.AssertEquals(1, dictionary.Count);
        }

        /// <summary>
        /// Find something not present with TryGetValue
        /// </summary>
        [Test]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new WeakValueDictionary<string, string>();

            string v;
            bool result = dictionary.TryGetValue("x", out v);

            Assert.IsFalse(result);
            Assert.IsNull(v);
            Assert.IsFalse(dictionary.Contains("x"));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [Test]
        public void EqualityComparer()
        {
            string k1 = "key";
            string v1 = "value";

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.IsFalse(Object.ReferenceEquals(k1, k2));

            string v2 = dictionary[k2];

            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [Test]
        public void SpecifiedEqualityComparer()
        {
            string k1 = "key";
            string v1 = "value";

            var dictionary = new WeakValueDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary[k1] = v1;

            string v2 = dictionary["KEY"];

            Assertion.AssertEquals(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Verify dictionary holds onto keys, but not values.
        /// </summary>
        [Test]
        public void OnlyValuesCollectable()
        {
            long memory0 = GC.GetTotalMemory(true);

            string k1 = new string('k', BigMemoryFootprintTest);
            string v1 = new string('v', BigMemoryFootprintTest);

            // Each character is 2 bytes, so about 4MB of this should be the strings
            long memory1 = GC.GetTotalMemory(true);

            var dictionary = new WeakValueDictionary<string, string>();
            dictionary[k1] = v1;

            k1 = null;

            long memory2 = GC.GetTotalMemory(true);

            // Key not collected, should be about the same 
            long difference = memory1 - memory2;

            Console.WriteLine("Before {0} Start {1}, end {2}, diff {3}, {4} more than start", memory0, memory1, memory2, difference, memory2 - memory0);
            Assertion.AssertEquals(true, difference < 500000); // big noise allowance

            v1 = null;

            memory2 = GC.GetTotalMemory(true);

            // Value collected, should be about 2MB less
            difference = memory1 - memory2;

            Console.WriteLine("Before {0} Start {1}, end {2}, diff {3}, {4} more than start", memory0, memory1, memory2, difference, memory2 - memory0); 
            Assertion.AssertEquals(true, difference > 1500000); // 2MB minus big noise allowance

            // This line is VERY important, as it keeps the GC from being too smart and collecting
            // the dictionary and its large strings because we never use them again.  
            GC.KeepAlive(dictionary);
        }

        /// <summary>
        /// Call Scavenge explicitly
        /// </summary>
        [Test]
        public void ExplicitScavenge()
        {
            object k1 = new object();
            object v1 = new object();

            var dictionary = new WeakValueDictionary<object, object>();
            dictionary[k1] = v1;

            Assertion.AssertEquals(1, dictionary.Count);

            v1 = null;
            GC.Collect();

            dictionary.Scavenge();

            Assertion.AssertEquals(0, dictionary.Count);
        }

        /// <summary>
        /// Growing should invoke Scavenge
        /// </summary>
        [Test]
        public void ScavengeOnGrow()
        {
            var dictionary = new WeakValueDictionary<object, object>();

            for (int i = 0; i < 100; i++)
            {
                dictionary[new Object()] = new Object();

                // Randomly collect some
                if (i == 15)
                {
                    GC.Collect();
                }
            }

            // We should have scavenged at least once
            Console.WriteLine("Count {0}", dictionary.Count);
            Assertion.AssertEquals(true, dictionary.Count < 100);

            // Finish with explicit scavenge
            int count1 = dictionary.Count;
            int removed = dictionary.Scavenge();
            int count2 = dictionary.Count;

            Console.WriteLine("Removed {0}", removed);
            Assertion.AssertEquals(removed, count1 - count2);
        }
    }
}

     