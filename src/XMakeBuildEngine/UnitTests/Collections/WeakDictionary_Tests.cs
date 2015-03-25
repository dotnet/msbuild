// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the weak dictionary class</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Collections;
using NUnit.Framework;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using System.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the weak dictionary class
    /// </summary>
    [TestFixture]
    public class WeakDictionary_Tests
    {
        /// <summary>
        /// Find with the same key inserted using the indexer
        /// </summary>
        [Test]
        public void Indexer_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2 = dictionary[k1];

            Assert.AreEqual(true, Object.ReferenceEquals(v1, v2));
            Assert.AreEqual(true, dictionary.Contains(k1));
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [Test]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Indexer_NotFound()
        {
            var dictionary = new WeakDictionary<object, object>();
            object value = dictionary[new Object()];
        }

        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [Test]
        public void TryGetValue_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assert.AreEqual(true, result);
            Assert.AreEqual(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find something not present with TryGetValue
        /// </summary>
        [Test]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new WeakDictionary<object, object>();

            object v;
            bool result = dictionary.TryGetValue(new Object(), out v);

            Assert.AreEqual(false, result);
            Assert.AreEqual(null, v);
            Assert.AreEqual(false, dictionary.Contains(new Object()));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [Test]
        public void EqualityComparer()
        {
            string k1 = String.Concat("ke", "y");
            object v1 = new Object();

            var dictionary = new WeakDictionary<string, object>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.AreEqual(false, Object.ReferenceEquals(k1, k2));

            object v2 = dictionary[k2];

            Assert.AreEqual(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// If value target has been collected, key should not be present.
        /// (When accessed, if target is null, entry is removed instead of returned.)
        /// </summary>
        [Test]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void IndexerRemovesDeadValue()
        {
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            object k = new Object();
            object v = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k] = v;

            v = null;
            GC.Collect();

            object value = dictionary[k]; // throws
        }

        /// <summary>
        /// If value target has been collected, key should not be present.
        /// (When accessed, if target is null, entry is removed instead of returned.)
        /// </summary>
        [Test]
        public void ContainsRemovesDeadValue()
        {
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            Console.WriteLine("Fixed contains test ..");

            Object k = new Object();
            object v = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k] = v;

            // Do not put an assert here! It will cause the test to mysteriously fail
            // as somehow an NUnit Assert can hold onto the value!
            v = null;
            GC.Collect();

            Assert.AreEqual(false, dictionary.Contains(k));
        }

        /// <summary>
        /// If value target has been collected, key should not be present.
        /// (When accessed, if target is null, entry is removed instead of returned.)
        /// </summary>
        [Test]
        public void TryGetRemovesDeadValue()
        {
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            object k = new Object();
            object v = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k] = v;

            // Do not put an assert here! It will cause the test to mysteriously fail
            // as somehow an NUnit Assert can hold onto the value!
            v = null;
            GC.Collect();

            object value;
            Assert.AreEqual(false, dictionary.TryGetValue(k, out value));
            Assert.AreEqual(0, dictionary.Count);
        }

        /// <summary>
        /// Verify dictionary doesn't hold onto keys
        /// </summary>
        [Test]
        public void KeysCollectable()
        {
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            string k1 = new string('k', 1000000);
            string v1 = new string('v', 1000000);

            // Each character is 2 bytes, so about 4MB of this should be the strings
            long memory1 = GC.GetTotalMemory(true);

            var dictionary = new WeakDictionary<string, string>();
            dictionary[k1] = v1;

            k1 = null;

            long memory2 = GC.GetTotalMemory(true);

            // Key collected, sould be about 2MB less
            long difference = memory1 - memory2;

            Console.WriteLine("Start {0}, end {1}, diff {2}", memory1, memory2, difference);
            Assert.AreEqual(true, difference > 1500000); // 2MB minus big noise allowance

            // This line is VERY important, as it keeps the GC from being too smart and collecting
            // the dictionary and its large strings because we never use them again.  
            GC.KeepAlive(dictionary);
        }

        /// <summary>
        /// Verify dictionary doesn't hold onto values
        /// </summary>
        [Test]
        public void ValuesCollectable()
        {
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            string k1 = new string('k', 1000000);
            string v1 = new string('v', 1000000);

            // Each character is 2 bytes, so about 4MB of this should be the strings
            long memory1 = GC.GetTotalMemory(true);

            var dictionary = new WeakDictionary<string, string>();
            dictionary[k1] = v1;

            v1 = null;

            long memory2 = GC.GetTotalMemory(true);

            // Value collected, sould be about 2MB less
            long difference = memory1 - memory2;

            Console.WriteLine("Start {0}, end {1}, diff {2}", memory1, memory2, difference);
            Assert.AreEqual(true, difference > 1500000); // 2MB minus big noise allowance

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
            if (NativeMethodsShared.IsMono)
            {
                Assert.Ignore("Mono has conservative GC, does not remove weak references");
            }

            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new WeakDictionary<object, object>();
            dictionary[k1] = v1;

            Assert.AreEqual(1, dictionary.Count);

            k1 = null;
            GC.Collect();

            dictionary.Scavenge();

            Assert.AreEqual(0, dictionary.Count);
        }

        /// <summary>
        /// Growing should invoke Scavenge
        /// </summary>
        [Test]
        public void ScavengeOnGrow()
        {
            var dictionary = new WeakDictionary<object, object>();

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
            Assert.AreEqual(true, dictionary.Count < 100);

            // Finish with explicit scavenge
            int count1 = dictionary.Count;
            int removed = dictionary.Scavenge();
            int count2 = dictionary.Count;

            Console.WriteLine("Removed {0}", removed);
            Assert.AreEqual(removed, count1 - count2);
        }
    }
}