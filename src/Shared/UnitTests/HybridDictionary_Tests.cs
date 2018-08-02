// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Collections;

using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the HybridDictionary.  Most of the more interesting tests are handled by the CopyOnWriteDictionary tests
    /// which use this as a backing store.
    /// </summary>
    public class HybridDictionary_Tests
    {
        /// <summary>
        /// Tests usage of the major functions.
        /// </summary>
        [Fact]
        public void TestUsage()
        {
            var dict = new HybridDictionary<int, string>();
            for (int i = 1; i < HybridDictionary<int, string>.MaxListSize + 2; i++)
            {
                dict[i] = (i * 2).ToString();

                // Verify the entry exists.
                Assert.True(dict.ContainsKey(i));

                // Verify the count has increased.
                Assert.Equal(dict.Count, i);

                // Verify the correct item was added
                Assert.Equal(dict[i], (i * 2).ToString());

                // Verify we don't incorrectly find non-existent items
                Assert.False(dict.ContainsKey(i + 10000));
                try
                {
                    string x = dict[i + 10000];
                }
                catch (Exception e)
                {
                    if (e.GetType() != typeof(KeyNotFoundException))
                    {
                        throw;
                    }
                }

                // Verify we can change the entry
                dict[i] = (i * 3).ToString();
                Assert.Equal(dict[i], (i * 3).ToString());
            }
        }

        /// <summary>
        /// Tests usage by random activities, comparing matching regular dictionary
        /// </summary>
        [Fact]
        public void Medley()
        {
            string keys = "AAAAAAABCDEFGabcdefg";

            Random rand = new Random();

            for (int capacity = 0; capacity < HybridDictionary<string, string>.MaxListSize + 2; capacity++)
            {
                var dict = new HybridDictionary<string, string>(capacity, StringComparer.OrdinalIgnoreCase);
                var shadow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < 2000; i++)
                {
                    switch (rand.Next(10 + 1))
                    {
                        case 0:
                            // Set something
                            if (shadow.Count < HybridDictionary<string, string>.MaxListSize + 2) // Don't bother exploring above here
                            {
                                string key = new String(keys[rand.Next(keys.Length)], 1);
                                string value = rand.Next(10).ToString();
                                dict[key] = value;
                                shadow[key] = value.ToString();
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 1:
                            // Remove something existing
                            if (shadow.Count > 0)
                            {
                                var entry = shadow.ElementAt(rand.Next(shadow.Count - 1)).Key;
                                Assert.Equal(dict.Remove(entry), shadow.Remove(entry));
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 2:
                            // Remove something nonexistent
                            AssertBothOrNeitherThrew<bool>(delegate () { return dict.Remove("ZZ"); }, delegate () { return shadow.Remove("ZZ"); });
                            AssertDictionariesIdentical(dict, shadow);
                            break;

                        case 3:
                            // Look up something existing
                            if (shadow.Count > 0)
                            {
                                var entry2 = shadow.ElementAt(rand.Next(shadow.Count - 1)).Key;
                                Assert.Equal(dict[entry2], shadow[entry2]);
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 4:
                            // Look up something non existing
                            AssertBothOrNeitherThrew<string>(delegate () { return dict["ZZ"]; }, delegate () { return shadow["ZZ"]; });
                            AssertDictionariesIdentical(dict, shadow);
                            break;

                        case 5:
                            // Try look up something existing
                            if (shadow.Count > 0)
                            {
                                var entry2 = shadow.ElementAt(rand.Next(shadow.Count - 1)).Key;
                                string value1;
                                string value2;
                                Assert.Equal(dict.TryGetValue(entry2, out value1), shadow.TryGetValue(entry2, out value2));
                                Assert.Equal(value1, value2);
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 6:
                            // Try look up something non existing
                            string value3;
                            string value4;
                            Assert.Equal(dict.TryGetValue("ZZ", out value3), shadow.TryGetValue("ZZ", out value4));
                            AssertDictionariesIdentical(dict, shadow);
                            break;

                        case 7:
                            dict.Clear();
                            shadow.Clear();
                            break;

                        case 8:
                            // Add something existing key same value
                            if (shadow.Count > 0)
                            {
                                var entry = shadow.ElementAt(rand.Next(shadow.Count - 1));
                                AssertBothOrNeitherThrew(delegate () { dict.Add(entry.Key, entry.Value); }, delegate () { shadow.Add(entry.Key, entry.Value); });
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 9:
                            // Add something existing key different value
                            if (shadow.Count > 0)
                            {
                                var key = shadow.ElementAt(rand.Next(shadow.Count - 1)).Key;
                                string value = rand.Next(10).ToString();
                                AssertBothOrNeitherThrew(delegate () { dict.Add(key, value); }, delegate () { shadow.Add(key, value); });
                                AssertDictionariesIdentical(dict, shadow);
                            }

                            break;

                        case 10:
                            // Add something non existing
                            string key2 = new String(keys[rand.Next(keys.Length)], 1);
                            string value5 = rand.Next(10).ToString();
                            AssertBothOrNeitherThrew(delegate () { dict.Add(key2, value5); }, delegate () { shadow.Add(key2, value5); });
                            AssertDictionariesIdentical(dict, shadow);
                            break;
                    }
                }

                dict.Clear();
                Assert.Equal(0, dict.Count);
            }
        }

        [Fact]
        private void VerifyHybridDictionaryBaseIndexer()
        {
            var dict = new HybridDictionary<string, string>();
            dict[(object) "key"] = "value";

            Assert.Equal("value", dict["key"]);
            Assert.Equal("value", dict[(object)"key"]);
            Assert.Equal("key", dict.Keys.First());
        }

        /// <summary>
        /// Performs both actions supplied and asserts either both or neither threw
        /// </summary>
        private void AssertBothOrNeitherThrew(Action one, Action two)
        {
            AssertBothOrNeitherThrew<object>(
                delegate ()
                {
                    one();
                    return null;
                },
                delegate ()
                {
                    two();
                    return null;
                }

                );
        }

        /// <summary>
        /// Performs both functions supplied and verifies either both or neither threw
        /// </summary>
        /// <typeparam name="R">Type</typeparam>
        private void AssertBothOrNeitherThrew<R>(Func<R> one, Func<R> two)
        {
            Exception caughtOne = null;
            Exception caughtTwo = null;
            R result1 = default(R);
            R result2 = default(R);

            try
            {
                result1 = one();
            }
            catch (Exception ex)
            {
                caughtOne = ex;
            }

            try
            {
                result2 = two();
            }
            catch (Exception ex)
            {
                caughtTwo = ex;
            }

            if (caughtOne != null ^ caughtTwo != null)
            {
                Assert.True(false, "One threw KNF exception, the other didn't");
            }

            Assert.Equal<R>(result1, result2); // "One returned {0} the other {1}", result1, result2
        }

        /// <summary>
        /// Verify the two dictionaries have identical content
        /// </summary>
        /// <typeparam name="K">key</typeparam>
        /// <typeparam name="V">value</typeparam>
        private void AssertDictionariesIdentical<K, V>(IDictionary<K, V> one, IDictionary<K, V> two)
        {
            Assert.Equal(one.Count, two.Count); // "Counts are unequal, {0} and {1}", one.Count, two.Count

            foreach (KeyValuePair<K, V> entry in one)
            {
                V value;
                if (!two.TryGetValue(entry.Key, out value))
                {
                    Assert.True(false, string.Format("one had key {0} the other didn't", entry.Key));
                }

                if (!value.Equals(entry.Value))
                {
                    Assert.True(false, string.Format("one had {0}={1} the other {2}={3}", entry.Key, entry.Value, entry.Key, value));
                }
            }

            foreach (var key in one.Keys)
            {
                Assert.Equal(true, two.ContainsKey(key));
            }

            var oneValues = new List<V>(one.Values);
            var twoValues = new List<V>(two.Values);

            oneValues.Sort();
            twoValues.Sort();

            Assert.Equal(oneValues.Count, twoValues.Count); //"Value counts are unequal, {0} and {1}", oneValues.Count, twoValues.Count

            for (int i = 0; i < oneValues.Count; i++)
            {
                Assert.Equal(oneValues[i], twoValues[i]);
            }

            // Now repeat, using IDictionary interface
            IDictionary oneId = (IDictionary)one;
            IDictionary twoId = (IDictionary)two;

            Assert.Equal(oneId.Count, twoId.Count); // "Counts are unequal, {0} and {1}", oneId.Count, twoId.Count

            foreach (DictionaryEntry entry in oneId)
            {
                if (!twoId.Contains(entry.Key))
                {
                    Assert.True(false, string.Format("oneId had key {0} the other didn't", entry.Key));
                }

                if (!entry.Value.Equals(twoId[entry.Key]))
                {
                    Assert.True(false, string.Format("oneId had {0}={1} the other {2}={3}", entry.Key, entry.Value, entry.Key, twoId[entry.Key]));
                }
            }

            foreach (var key in oneId.Keys)
            {
                Assert.Equal(true, twoId.Contains(key));
            }

            var oneIdValues = new ArrayList(oneId.Values);
            var twoIdValues = new ArrayList(twoId.Values);

            oneIdValues.Sort();
            twoIdValues.Sort();

            Assert.Equal(oneIdValues.Count, twoIdValues.Count); // "Value counts are unequal, {0} and {1}", oneIdValues.Count, twoIdValues.Count

            for (int i = 0; i < oneIdValues.Count; i++)
            {
                Assert.Equal(oneIdValues[i], twoIdValues[i]);
            }
        }
    }
}
