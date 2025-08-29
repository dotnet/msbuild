// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the multi-dictionary class
    /// </summary>
    public class MultiDictionary_Tests
    {
        /// <summary>
        /// Empty dictionary
        /// </summary>
        [Fact]
        public void Empty()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Assert.Equal(0, dictionary.KeyCount);
            Assert.Equal(0, dictionary.ValueCount);

            Assert.False(dictionary.Remove("x", "y"));

            foreach (string value in dictionary["x"])
            {
                Assert.True(false);
            }
        }

        /// <summary>
        /// Remove stuff that is there
        /// </summary>
        [Fact]
        public void Remove()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            Assert.True(dictionary.Remove("x", "x1"));

            Assert.Equal(2, dictionary.KeyCount);
            Assert.Equal(2, dictionary.ValueCount);

            Assert.True(dictionary.Remove("x", "x2"));

            Assert.Equal(1, dictionary.KeyCount);
            Assert.Equal(1, dictionary.ValueCount);

            Assert.True(dictionary.Remove("y", "y1"));

            Assert.Equal(0, dictionary.KeyCount);
            Assert.Equal(0, dictionary.ValueCount);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");

            Assert.True(dictionary.Remove("x", "x2"));

            Assert.Equal(1, dictionary.KeyCount);
            Assert.Equal(1, dictionary.ValueCount);
        }

        /// <summary>
        /// Remove stuff that isn't there
        /// </summary>
        [Fact]
        public void RemoveNonExistent()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            Assert.False(dictionary.Remove("z", "y1"));
            Assert.False(dictionary.Remove("x", "y1"));
            Assert.False(dictionary.Remove("y", "y2"));

            Assert.Equal(2, dictionary.KeyCount);
            Assert.Equal(3, dictionary.ValueCount);
        }

        /// <summary>
        /// Enumerate over all values for a key
        /// </summary>
        [Fact]
        public void Enumerate()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            List<string> values = Helpers.MakeList<string>(dictionary["x"]);
            values.Sort();

            Assert.Equal(2, values.Count);
            Assert.Equal("x1", values[0]);
            Assert.Equal("x2", values[1]);

            values = Helpers.MakeList<string>(dictionary["y"]);

            Assert.Single(values);
            Assert.Equal("y1", values[0]);

            values = Helpers.MakeList<string>(dictionary["z"]);

            Assert.Empty(values);
        }

        /// <summary>
        /// Mixture of adds and removes
        /// </summary>
        [Fact]
        public void MixedAddRemove()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Remove("x", "x1");
            dictionary.Add("x", "x1");
            dictionary.Add("x", "x1");
            dictionary.Add("x", "x1");
            dictionary.Remove("x", "x1");
            dictionary.Remove("x", "x1");
            dictionary.Remove("x", "x1");
            dictionary.Add("x", "x2");

            Assert.Equal(1, dictionary.KeyCount);
            Assert.Equal(1, dictionary.ValueCount);

            List<string> values = Helpers.MakeList<string>(dictionary["x"]);

            Assert.Single(values);
            Assert.Equal("x2", values[0]);
        }

        /// <summary>
        /// Clearing out
        /// </summary>
        [Fact]
        public void Clear()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            dictionary.Clear();

            Assert.Equal(0, dictionary.KeyCount);
            Assert.Equal(0, dictionary.ValueCount);
        }
    }
}
