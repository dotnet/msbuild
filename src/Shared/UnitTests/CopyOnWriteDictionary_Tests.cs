// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Xunit;
using Shouldly;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the CopyOnWriteDictionary 
    /// </summary>
    public class CopyOnWriteDictionary_Tests
    {
        /// <summary>
        /// Find with the same key inserted using the indexer
        /// </summary>
        [Fact]
        public void Indexer_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2 = dictionary[k1];

            Assert.True(Object.ReferenceEquals(v1, v2));
            Assert.True(dictionary.ContainsKey(k1));
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [Fact]
        public void Indexer_NotFound()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                var dictionary = new CopyOnWriteDictionary<object, object>();
                object value = dictionary[new Object()];
            }
           );
        }
        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [Fact]
        public void TryGetValue_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assert.True(result);
            Assert.True(Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find something not present with TryGetValue
        /// </summary>
        [Fact]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new CopyOnWriteDictionary<object, object>();

            object v;
            bool result = dictionary.TryGetValue(new Object(), out v);

            Assert.False(result);
            Assert.Null(v);
            Assert.False(dictionary.ContainsKey(new Object()));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [Fact]
        public void EqualityComparer()
        {
            string k1 = String.Concat("ke", "y");
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<string, object>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.False(Object.ReferenceEquals(k1, k2));

            object v2 = dictionary[k2];

            Assert.True(Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Cloning sees the same values 
        /// </summary>
        [Fact]
        public void CloneVisibility()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.Equal("1", dictionary["test"]);

            var clone = dictionary.Clone();

            Assert.Equal("1", clone["test"]);
            Assert.Equal(clone.Count, dictionary.Count);
        }

        /// <summary>
        /// Clone uses same comparer 
        /// </summary>
        [Fact]
        public void CloneComparer()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary["test"] = "1";
            Assert.Equal("1", dictionary["test"]);

            var clone = dictionary.Clone();

            Assert.Equal("1", clone["TEST"]);
        }

        /// <summary>
        /// Writes to original not visible to clone
        /// </summary>
        [Fact]
        public void OriginalWritesNotVisibleToClones()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
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
        [Fact]
        public void CloneWritesNotVisibleToOriginal()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.Equal("1", dictionary["test"]);

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            Assert.True(dictionary.HasSameBacking(clone));
            Assert.True(dictionary.HasSameBacking(clone2));

            clone["test"] = "2";
            Assert.False(dictionary.HasSameBacking(clone));
            Assert.False(clone2.HasSameBacking(clone));
            Assert.True(dictionary.HasSameBacking(clone2));

            clone2["test"] = "3";
            Assert.False(dictionary.HasSameBacking(clone2));

            Assert.Equal("1", dictionary["test"]);
            Assert.Equal("2", clone["test"]);
        }

        /// <summary>
        /// Serialize basic case
        /// </summary>
        [Fact]
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

                Assert.Equal(dictionary.Count, dictionary2.Count);
                Assert.Equal(dictionary.Comparer, dictionary2.Comparer);
                Assert.Equal("1", dictionary2[1]);

                dictionary2.Add(2, "2");
            }
        }

        /// <summary>
        /// Serialize custom comparer
        /// </summary>
        [Fact]
        public void SerializeDeserialize2()
        {
            CopyOnWriteDictionary<string, string> dictionary = new CopyOnWriteDictionary<string, string>(MSBuildNameIgnoreCaseComparer.Default);

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, dictionary);
                stream.Position = 0;

                CopyOnWriteDictionary<string, string> deserialized = (CopyOnWriteDictionary<string, string>)formatter.Deserialize(stream);

                deserialized.Count.ShouldBe(dictionary.Count);
                deserialized.Comparer.ShouldBeOfType<MSBuildNameIgnoreCaseComparer>();
            }
        }
    }
}
