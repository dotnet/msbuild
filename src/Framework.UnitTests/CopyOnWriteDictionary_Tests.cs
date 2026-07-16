// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Shouldly;

#nullable disable

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
        [MSBuildTestMethod]
        public void Indexer_ReferenceFound()
        {
            string k1 = new string(nameof(Indexer_ReferenceFound).ToCharArray()); // force create new string
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2 = dictionary[k1];

            Assert.IsTrue(Object.ReferenceEquals(v1, v2));
            Assert.IsTrue(dictionary.ContainsKey(k1));
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [MSBuildTestMethod]
        public void Indexer_NotFound()
        {
            Assert.ThrowsExactly<KeyNotFoundException>(() =>
            {
                var dictionary = new CopyOnWriteDictionary<object>();
                object value = dictionary[string.Empty];
            });
        }
        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [MSBuildTestMethod]
        public void TryGetValue_ReferenceFound()
        {
            string k1 = new string(nameof(TryGetValue_ReferenceFound).ToCharArray());
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assert.IsTrue(result);
            Assert.IsTrue(Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find something not present with TryGetValue
        /// </summary>
        [MSBuildTestMethod]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new CopyOnWriteDictionary<object>();

            object v;
            bool result = dictionary.TryGetValue(string.Empty, out v);

            Assert.IsFalse(result);
            Assert.IsNull(v);
            Assert.IsFalse(dictionary.ContainsKey(string.Empty));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [MSBuildTestMethod]
        public void EqualityComparer()
        {
            string k1 = String.Concat("ke", "y");
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.IsFalse(Object.ReferenceEquals(k1, k2));

            object v2 = dictionary[k2];

            Assert.IsTrue(Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Cloning sees the same values
        /// </summary>
        [MSBuildTestMethod]
        public void CloneVisibility()
        {
            var dictionary = new CopyOnWriteDictionary<string>();
            dictionary["test"] = "1";
            Assert.AreEqual("1", dictionary["test"]);

            var clone = dictionary.Clone();

            Assert.AreEqual("1", clone["test"]);
            Assert.AreEqual(clone.Count, dictionary.Count);
        }

        /// <summary>
        /// Clone uses same comparer
        /// </summary>
        [MSBuildTestMethod]
        public void CloneComparer()
        {
            var dictionary = new CopyOnWriteDictionary<string>(StringComparer.OrdinalIgnoreCase);
            dictionary["test"] = "1";
            Assert.AreEqual("1", dictionary["test"]);

            var clone = dictionary.Clone();

            Assert.AreEqual("1", clone["TEST"]);
        }

        /// <summary>
        /// Writes to original not visible to clone
        /// </summary>
        [MSBuildTestMethod]
        public void OriginalWritesNotVisibleToClones()
        {
            var dictionary = new CopyOnWriteDictionary<string>();
            dictionary["test"] = "1";
            dictionary["test"].ShouldBe("1");

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            dictionary.HasSameBacking(clone).ShouldBeTrue();
            dictionary.HasSameBacking(clone2).ShouldBeTrue();

            dictionary["test"] = "2";

            dictionary.HasSameBacking(clone).ShouldBeFalse();
            dictionary.HasSameBacking(clone2).ShouldBeFalse();
            clone.HasSameBacking(clone2).ShouldBeTrue();

            clone["test"].ShouldBe("1");
            clone2["test"].ShouldBe("1");
        }

        /// <summary>
        /// Writes to clone not visible to original
        /// </summary>
        [MSBuildTestMethod]
        public void CloneWritesNotVisibleToOriginal()
        {
            var dictionary = new CopyOnWriteDictionary<string>();
            dictionary["test"] = "1";
            Assert.AreEqual("1", dictionary["test"]);

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

            Assert.AreEqual("1", dictionary["test"]);
            Assert.AreEqual("2", clone["test"]);
        }
    }
}
