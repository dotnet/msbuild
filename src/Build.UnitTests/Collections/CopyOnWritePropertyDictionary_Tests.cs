// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Collections;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for <see cref="CopyOnWritePropertyDictionary{T}"/>.
    /// </summary>
    public class CopyOnWritePropertyDictionary_Tests
    {
        [Fact]
        public void Count()
        {
            var dic = CreateInstance();

            Assert.Equal(0, dic.Count);

            dic.Set(new("a"));

            Assert.Equal(1, dic.Count);

            dic.Set(new("b"));

            Assert.Equal(2, dic.Count);

            dic.Set(new("c"));

            Assert.Equal(3, dic.Count);

            dic.Clear();

            Assert.Equal(0, dic.Count);
        }

        [Fact]
        public void Indexer()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            Assert.Null(dic["a"]);
            Assert.Null(dic["b"]);

            dic["a"] = a;

            Assert.Same(a, dic["a"]);
            Assert.Null(dic["b"]);

            dic["b"] = b;

            Assert.Same(a, dic["a"]);
            Assert.Same(b, dic["b"]);

            // Cannot set a null value
            Assert.ThrowsAny<Exception>(() => dic["a"] = null);

            // Value's key must match the specified key
            Assert.ThrowsAny<Exception>(() => dic["a"] = b);
        }

        [Fact]
        public void Contains()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            Assert.False(dic.Contains("a"));
            Assert.False(dic.Contains("b"));

            dic["a"] = a;

            Assert.True(dic.Contains("a"));
            Assert.False(dic.Contains("b"));

            dic["b"] = b;

            Assert.True(dic.Contains("a"));
            Assert.True(dic.Contains("b"));
        }

        [Fact]
        public void Clear()
        {
            var dic = CreateInstance("a", "b", "c");

            Assert.Equal(3, dic.Count);

            dic.Clear();

            Assert.Equal(0, dic.Count);
        }

        [Fact]
        public void Enumeration()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            dic.Set(a);
            dic.Set(b);

            dic.ShouldBeSetEquivalentTo(new[] { a, b });
        }

        [Fact]
        public void Equal()
        {
            var dic1 = CreateInstance("a", "b", "c");
            var dic2 = CreateInstance("a", "b", "c");
            var dic3 = CreateInstance("c", "b", "a");      // reversed order
            var dic4 = CreateInstance("a", "b");           // missing item
            var dic5 = CreateInstance("a", "b", "c", "d"); // extra item

            Test(dic1, dic1, true);
            Test(dic1, dic2, true);
            Test(dic1, dic3, true);

            Test(dic1, dic4, false);
            Test(dic1, dic5, false);

            static void Test(CopyOnWritePropertyDictionary<MockValue> a, CopyOnWritePropertyDictionary<MockValue> b, bool expected)
            {
                if (expected)
                {
                    // Test equality in both directions
                    Assert.Equal(a, b);
                    Assert.Equal(b, a);
                }
                else
                {
                    // Test equality in both directions
                    Assert.NotEqual(a, b);
                    Assert.NotEqual(b, a);
                }
            }
        }

        [Fact]
        public void Remove()
        {
            var dic = CreateInstance("a", "b", "c");

            Assert.False(dic.Remove("ZZZ"));

            Assert.True(dic.Remove("a"));
            Assert.False(dic.Remove("a"));
            Assert.True(dic.Remove("b"));
            Assert.True(dic.Remove("c"));

            Assert.Equal(0, dic.Count);
        }

        [Fact]
        public void ImportProperties()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            dic.ImportProperties(new[] { a, b });

            dic.ShouldBeSetEquivalentTo(new[] { a, b });
        }

        [Fact]
        public void DeepClone()
        {
            CopyOnWritePropertyDictionary<MockValue> source = CreateInstance("a", "b", "c");
            CopyOnWritePropertyDictionary<MockValue> clone = source.DeepClone();

            Assert.Equal(source, clone);
            Assert.NotSame(source, clone);
        }

        private static CopyOnWritePropertyDictionary<MockValue> CreateInstance(params string[] values)
        {
            CopyOnWritePropertyDictionary<MockValue> dic = new CopyOnWritePropertyDictionary<MockValue>();

            foreach (string value in values)
            {
                dic.Set(new(value));
            }

            return dic;
        }

        private sealed class MockValue : IKeyed, IValued, IEquatable<MockValue>, IImmutable
        {
            public MockValue(string s) => Key = s;

            public string Key { get; }

            public string EscapedValue => Key;

            public bool Equals(MockValue other)
            {
                return other != null && Key == other.Key;
            }
        }
    }
}
