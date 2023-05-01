// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;

using Shouldly;

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

            dic.Count.ShouldBe(0);

            dic.Set(new("a"));

            dic.Count.ShouldBe(1);

            dic.Set(new("b"));

            dic.Count.ShouldBe(2);

            dic.Set(new("c"));

            dic.Count.ShouldBe(3);

            dic.Clear();

            dic.Count.ShouldBe(0);
        }

        [Fact]
        public void Indexer()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            dic["a"].ShouldBeNull();
            dic["b"].ShouldBeNull();

            dic["a"] = a;

            dic["a"].ShouldBeSameAs(a);
            dic["b"].ShouldBeNull();

            dic["b"] = b;

            dic["a"].ShouldBeSameAs(a);
            dic["b"].ShouldBeSameAs(b);

            // Cannot set a null value
            Assert.ThrowsAny<Exception>(() => dic["a"] = null!);

            // Value's key must match the specified key
            Assert.ThrowsAny<Exception>(() => dic["a"] = b);
        }

        [Fact]
        public void Contains()
        {
            var dic = CreateInstance();

            MockValue a = new("a");
            MockValue b = new("b");

            dic.Contains("a").ShouldBeFalse();
            dic.Contains("b").ShouldBeFalse();

            dic["a"] = a;

            dic.Contains("a").ShouldBeTrue();
            dic.Contains("b").ShouldBeFalse();

            dic["b"] = b;

            dic.Contains("a").ShouldBeTrue();
            dic.Contains("b").ShouldBeTrue();
        }

        [Fact]
        public void Clear()
        {
            var dic = CreateInstance("a", "b", "c");

            dic.Count.ShouldBe(3);

            dic.Clear();

            dic.Count.ShouldBe(0);
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
                    a.ShouldBe(b);
                    b.ShouldBe(a);
                }
                else
                {
                    // Test equality in both directions
                    a.ShouldNotBe(b);
                    b.ShouldNotBe(a);
                }
            }
        }

        [Fact]
        public void Remove()
        {
            var dic = CreateInstance("a", "b", "c");

            dic.Remove("ZZZ").ShouldBeFalse();

            dic.Remove("a").ShouldBeTrue();
            dic.Remove("a").ShouldBeFalse();
            dic.Remove("b").ShouldBeTrue();
            dic.Remove("c").ShouldBeTrue();

            dic.Count.ShouldBe(0);
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

            source.ShouldBe(clone);
            source.ShouldNotBeSameAs(clone);
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

        private sealed class MockValue : IKeyed, IValued, IEquatable<MockValue>, IEquatable<object>, IImmutable
        {
            public MockValue(string s) => Key = s;

            public string Key { get; }

            public string EscapedValue => Key;

            public bool Equals(MockValue? other)
            {
                return other != null && Key == other.Key;
            }

            public new bool Equals(object? other)
            {
                if (other is MockValue mv)
                {
                    return Equals(mv);
                }
                return false;
            }
        }
    }
}
