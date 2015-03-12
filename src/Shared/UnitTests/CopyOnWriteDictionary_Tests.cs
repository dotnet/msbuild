// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the copy on write dictionary class</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the CopyOnWriteDictionary 
    /// </summary>
    [TestClass]
    public class CopyOnWriteDictionary_Tests
    {
        /// <summary>
        /// Find with the same key inserted using the indexer
        /// </summary>
        [TestMethod]
        public void Indexer_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2 = dictionary[k1];

            Assert.AreEqual(true, Object.ReferenceEquals(v1, v2));
            Assert.AreEqual(true, dictionary.ContainsKey(k1));
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Indexer_NotFound()
        {
            var dictionary = new CopyOnWriteDictionary<object, object>();
            object value = dictionary[new Object()];
        }

        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [TestMethod]
        public void TryGetValue_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
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
        [TestMethod]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new CopyOnWriteDictionary<object, object>();

            object v;
            bool result = dictionary.TryGetValue(new Object(), out v);

            Assert.AreEqual(false, result);
            Assert.AreEqual(null, v);
            Assert.AreEqual(false, dictionary.ContainsKey(new Object()));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [TestMethod]
        public void EqualityComparer()
        {
            string k1 = String.Concat("ke", "y");
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<string, object>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.AreEqual(false, Object.ReferenceEquals(k1, k2));

            object v2 = dictionary[k2];

            Assert.AreEqual(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Cloning sees the same values 
        /// </summary>
        [TestMethod]
        public void CloneVisibility()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.AreEqual(dictionary["test"], "1");

            var clone = dictionary.Clone();

            Assert.AreEqual(clone["test"], "1");
            Assert.AreEqual(clone.Count, dictionary.Count);
        }

        /// <summary>
        /// Clone uses same comparer 
        /// </summary>
        [TestMethod]
        public void CloneComparer()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary["test"] = "1";
            Assert.AreEqual(dictionary["test"], "1");

            var clone = dictionary.Clone();

            Assert.AreEqual(clone["TEST"], "1");
        }

        /// <summary>
        /// Writes to original not visible to clone
        /// </summary>
        [TestMethod]
        public void OriginalWritesNotVisibleToClones()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.AreEqual(dictionary["test"], "1");

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            Assert.IsTrue(dictionary.HasSameBacking(clone));
            Assert.IsTrue(dictionary.HasSameBacking(clone2));

            dictionary["test"] = "2";

            Assert.IsFalse(dictionary.HasSameBacking(clone));
            Assert.IsFalse(dictionary.HasSameBacking(clone2));
            Assert.IsTrue(clone.HasSameBacking(clone2));

            Assert.AreEqual(clone["test"], "1");
            Assert.AreEqual(clone2["test"], "1");
        }

        /// <summary>
        /// Writes to clone not visible to original
        /// </summary>
        [TestMethod]
        public void CloneWritesNotVisibleToOriginal()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.AreEqual(dictionary["test"], "1");

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            Assert.IsTrue(dictionary.HasSameBacking(clone));
            Assert.IsTrue(dictionary.HasSameBacking(clone2));

            clone["test"] = "2";
            Assert.IsFalse(dictionary.HasSameBacking(clone));
            Assert.IsFalse(clone2.HasSameBacking(clone));
            Assert.IsTrue(dictionary.HasSameBacking(clone2));

            clone2["test"] = "3";
            Assert.IsFalse(dictionary.HasSameBacking(clone2));

            Assert.AreEqual(dictionary["test"], "1");
            Assert.AreEqual(clone["test"], "2");
        }

        /// <summary>
        /// Serialize basic case
        /// </summary>
        [TestMethod]
        public void SerializeDeserialize()
        {
            CopyOnWriteDictionary<int, string> dictionary = new CopyOnWriteDictionary<int, string>();
            dictionary.Add(1, "1");

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, dictionary);
                stream.Position = 0;

                var dictionary2 = (CopyOnWriteDictionary<int, string>)formatter.Deserialize(stream);

                Assert.AreEqual(dictionary.Count, dictionary2.Count);
                Assert.AreEqual(dictionary.Comparer, dictionary2.Comparer);
                Assert.AreEqual("1", dictionary2[1]);

                dictionary2.Add(2, "2");
            }
        }

        /// <summary>
        /// Serialize custom comparer
        /// </summary>
        [TestMethod]
        public void SerializeDeserialize2()
        {
            CopyOnWriteDictionary<string, string> dictionary = new CopyOnWriteDictionary<string, string>(MSBuildNameIgnoreCaseComparer.Default);

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, dictionary);
                stream.Position = 0;

                CopyOnWriteDictionary<string, string> dictionary2 = (CopyOnWriteDictionary<string, string>)formatter.Deserialize(stream);

                Assert.AreEqual(dictionary.Count, dictionary2.Count);
                Assert.AreEqual(typeof(MSBuildNameIgnoreCaseComparer), dictionary2.Comparer.GetType());
            }
        }
    }
}
