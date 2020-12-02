// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// We don't automatically run these tests against the BCL implementation of ImmutableDictionary as it would require dual-compiling
// this file. When making changes to this test, though, it is recommended to run them manually by uncommenting the following line.
// This helps ensure that the real thing has the same behavior that we expect in our implementation.
//#define _TEST_BCL_IMMUTABLE_DICTIONARY

extern alias MSBuildTaskHost;

using System;
using System.Collections;
using System.Collections.Generic;

using Shouldly;
using Xunit;

#if _TEST_BCL_IMMUTABLE_DICTIONARY
using ImmutableDictionary = System.Collections.Immutable.ImmutableDictionary<string, string>;
#else
using ImmutableDictionary = MSBuildTaskHost::System.Collections.Immutable.ImmutableDictionary<string, string>;
#endif

namespace Microsoft.Build.UnitTests
{
    public class ImmutableDictionary_Tests
    {
        private readonly ImmutableDictionary _emptyDict = ImmutableDictionary.Empty;

        [Fact]
        public void SimplesBoolPropertiesReturnExpectedValues()
        {
            ((IDictionary)_emptyDict).IsFixedSize.ShouldBeTrue();
            ((IDictionary)_emptyDict).IsReadOnly.ShouldBeTrue();
            ((IDictionary)_emptyDict).IsSynchronized.ShouldBeTrue();
        }

        [Fact]
        public void CountReturnsExpectedValue()
        {
            _emptyDict.Count.ShouldBe(0);
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            dict.Count.ShouldBe(1);
            dict = dict.SetItem("Key2", "Value2");
            dict.Count.ShouldBe(2);
            dict = dict.Clear();
            dict.Count.ShouldBe(0);
        }

        [Fact]
        public void IndexerReturnsPreviouslySetItem()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            dict["Key1"].ShouldBe("Value1");
            ((IDictionary)dict)["Key1"].ShouldBe("Value1");
            ((IDictionary<string, string>)dict)["Key1"].ShouldBe("Value1");
        }

        [Fact]
        public void IndexerThrowsForItemNotPreviouslySet()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            Should.Throw<KeyNotFoundException>(() => _ = dict["Key2"]);
            Should.Throw<KeyNotFoundException>(() => _ = ((IDictionary)dict)["Key2"]);
            Should.Throw<KeyNotFoundException>(() => _ = ((IDictionary<string, string>)dict)["Key2"]);
        }

        [Fact]
        public void ContainsReturnsTrueForPeviouslySetItem()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            dict.Contains(new KeyValuePair<string, string>("Key1", "Value1")).ShouldBeTrue();
            dict.ContainsKey("Key1").ShouldBeTrue();
            ((IDictionary)dict).Contains("Key1").ShouldBeTrue();
        }

        [Fact]
        public void ContainsReturnsFalseForItemNotPeviouslySet()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            dict.Contains(new KeyValuePair<string, string>("Key2", "Value2")).ShouldBeFalse();
            dict.ContainsKey("Key2").ShouldBeFalse();
            ((IDictionary)dict).Contains("Key2").ShouldBeFalse();
        }

        [Fact]
        public void EnumeratorEnumeratesItems()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");

            IEnumerator<KeyValuePair<string, string>> enumerator1 = dict.GetEnumerator();
            int i = 0;
            while (enumerator1.MoveNext())
            {
                i++;
                enumerator1.Current.Key.ShouldBe("Key1");
                enumerator1.Current.Value.ShouldBe("Value1");
            }
            i.ShouldBe(dict.Count);

            IDictionaryEnumerator enumerator2 = ((IDictionary)dict).GetEnumerator();
            i = 0;
            while (enumerator2.MoveNext())
            {
                i++;
                enumerator2.Key.ShouldBe("Key1");
                enumerator2.Value.ShouldBe("Value1");
            }
            i.ShouldBe(dict.Count);

            IEnumerator enumerator3 = ((IEnumerable)dict).GetEnumerator();
            i = 0;
            while (enumerator3.MoveNext())
            {
                i++;
                KeyValuePair<string, string> entry = (KeyValuePair<string, string>)enumerator3.Current;
                entry.Key.ShouldBe("Key1");
                entry.Value.ShouldBe("Value1");
            }
            i.ShouldBe(dict.Count);
        }

        [Fact]
        public void CopyToCopiesItemsToArray()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");

            KeyValuePair<string, string>[] array1 = new KeyValuePair<string, string>[1];
            ((ICollection<KeyValuePair<string, string>>)dict).CopyTo(array1, 0);
            array1[0].Key.ShouldBe("Key1");
            array1[0].Value.ShouldBe("Value1");

            array1 = new KeyValuePair<string, string>[2];
            ((ICollection<KeyValuePair<string, string>>)dict).CopyTo(array1, 1);
            array1[1].Key.ShouldBe("Key1");
            array1[1].Value.ShouldBe("Value1");

            DictionaryEntry[] array2 = new DictionaryEntry[1];
            ((ICollection)dict).CopyTo(array2, 0);
            array2[0].Key.ShouldBe("Key1");
            array2[0].Value.ShouldBe("Value1");

            array2 = new DictionaryEntry[2];
            ((ICollection)dict).CopyTo(array2, 1);
            array2[1].Key.ShouldBe("Key1");
            array2[1].Value.ShouldBe("Value1");
        }

        [Fact]
        public void CopyToThrowsOnInvalidInput()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");

            Should.Throw<ArgumentNullException>(() => ((ICollection<KeyValuePair<string, string>>)dict).CopyTo(null, 0));
            Should.Throw<ArgumentNullException>(() => ((ICollection)dict).CopyTo(null, 0));

            KeyValuePair<string, string>[] array1 = new KeyValuePair<string, string>[1];
            DictionaryEntry[] array2 = new DictionaryEntry[1];
            Should.Throw<ArgumentOutOfRangeException>(() => ((ICollection<KeyValuePair<string, string>>)dict).CopyTo(array1, -1));
            Should.Throw<ArgumentOutOfRangeException>(() => ((ICollection)dict).CopyTo(array1, -1));

            Should.Throw<ArgumentException>(() => ((ICollection<KeyValuePair<string, string>>)dict).CopyTo(array1, 1));
            Should.Throw<ArgumentException>(() => ((ICollection)dict).CopyTo(array1, 1));
        }

        [Fact]
        public void KeysReturnsKeys()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");

            ICollection<string> keys1 = ((IDictionary<string, string>)dict).Keys;
            keys1.ShouldBe(new string[] { "Key1" });

            ICollection keys2 = ((IDictionary)dict).Keys;
            keys2.ShouldBe(new string[] { "Key1" });
        }

        [Fact]
        public void ValuesReturnsValues()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");

            ICollection<string> values1 = ((IDictionary<string, string>)dict).Values;
            values1.ShouldBe(new string[] { "Value1" });

            ICollection values2 = ((IDictionary)dict).Values;
            values2.ShouldBe(new string[] { "Value1" });
        }

        [Fact]
        public void SetItemReturnsNewInstanceAfterAdding()
        {
            ImmutableDictionary dict = _emptyDict.SetItem("Key1", "Value1");
            dict.ShouldNotBeSameAs(_emptyDict);
        }

        [Fact]
        public void SetItemReturnsNewInstanceAfterUpdating()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.SetItem("Key1", "Value2");
            dict2.ShouldNotBeSameAs(dict1);
        }

        [Fact]
        public void SetItemReturnsSameInstanceWhenItemAlreadyExists()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.SetItem("Key1", "Value1");
            dict2.ShouldBeSameAs(dict1);
        }

        [Fact]
        public void RemoveReturnsNewInstanceAfterDeleting()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.Remove("Key1");
            dict2.ShouldNotBeSameAs(dict1);
        }

        [Fact]
        public void RemoveReturnsSameInstanceWhenItemDoesNotExist()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.Remove("Key2");
            dict2.ShouldBeSameAs(dict1);
        }

        [Fact]
        public void ClearReturnsNewInstance()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.Clear();
            dict2.ShouldNotBeSameAs(dict1);
        }

        [Fact]
        public void WithComparersCreatesNewInstanceWithSpecifiedKeyComparer()
        {
            ImmutableDictionary dict1 = _emptyDict.SetItem("Key1", "Value1");
            ImmutableDictionary dict2 = dict1.WithComparers(StringComparer.OrdinalIgnoreCase);
            dict2["KEY1"].ShouldBe("Value1");
        }

        [Fact]
        public void AddRangeAddsAllItems()
        {
            ImmutableDictionary dict = _emptyDict.AddRange(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("Key1", "Value1"),
                new KeyValuePair<string, string>("Key2", "Value2")
            });
            dict.Count.ShouldBe(2);
            dict["Key1"].ShouldBe("Value1");
            dict["Key2"].ShouldBe("Value2");
        }
    }
}
