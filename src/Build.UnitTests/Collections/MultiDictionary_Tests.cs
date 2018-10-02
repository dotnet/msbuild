// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using System.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Xunit;

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

            Assert.Equal(false, dictionary.Remove("x", "y"));

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

            Assert.Equal(true, dictionary.Remove("x", "x1"));

            Assert.Equal(2, dictionary.KeyCount);
            Assert.Equal(2, dictionary.ValueCount);

            Assert.Equal(true, dictionary.Remove("x", "x2"));

            Assert.Equal(1, dictionary.KeyCount);
            Assert.Equal(1, dictionary.ValueCount);

            Assert.Equal(true, dictionary.Remove("y", "y1"));

            Assert.Equal(0, dictionary.KeyCount);
            Assert.Equal(0, dictionary.ValueCount);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");

            Assert.Equal(true, dictionary.Remove("x", "x2"));

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

            Assert.Equal(false, dictionary.Remove("z", "y1"));
            Assert.Equal(false, dictionary.Remove("x", "y1"));
            Assert.Equal(false, dictionary.Remove("y", "y2"));

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

            Assert.Equal(1, values.Count);
            Assert.Equal("y1", values[0]);

            values = Helpers.MakeList<string>(dictionary["z"]);

            Assert.Equal(0, values.Count);
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

            Assert.Equal(1, values.Count);
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