// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the multi-dictionary class</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    /// Tests for the multi-dictionary class
    /// </summary>
    [TestClass]
    public class MultiDictionary_Tests
    {
        /// <summary>
        /// Empty dictionary
        /// </summary>
        [TestMethod]
        public void Empty()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Assert.AreEqual(0, dictionary.KeyCount);
            Assert.AreEqual(0, dictionary.ValueCount);

            Assert.AreEqual(false, dictionary.Remove("x", "y"));

            foreach (string value in dictionary["x"])
            {
                Assert.Fail();
            }
        }

        /// <summary>
        /// Remove stuff that is there
        /// </summary>
        [TestMethod]
        public void Remove()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            Assert.AreEqual(true, dictionary.Remove("x", "x1"));

            Assert.AreEqual(2, dictionary.KeyCount);
            Assert.AreEqual(2, dictionary.ValueCount);

            Assert.AreEqual(true, dictionary.Remove("x", "x2"));

            Assert.AreEqual(1, dictionary.KeyCount);
            Assert.AreEqual(1, dictionary.ValueCount);

            Assert.AreEqual(true, dictionary.Remove("y", "y1"));

            Assert.AreEqual(0, dictionary.KeyCount);
            Assert.AreEqual(0, dictionary.ValueCount);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");

            Assert.AreEqual(true, dictionary.Remove("x", "x2"));

            Assert.AreEqual(1, dictionary.KeyCount);
            Assert.AreEqual(1, dictionary.ValueCount);
        }

        /// <summary>
        /// Remove stuff that isn't there
        /// </summary>
        [TestMethod]
        public void RemoveNonExistent()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            Assert.AreEqual(false, dictionary.Remove("z", "y1"));
            Assert.AreEqual(false, dictionary.Remove("x", "y1"));
            Assert.AreEqual(false, dictionary.Remove("y", "y2"));

            Assert.AreEqual(2, dictionary.KeyCount);
            Assert.AreEqual(3, dictionary.ValueCount);
        }

        /// <summary>
        /// Enumerate over all values for a key
        /// </summary>
        [TestMethod]
        public void Enumerate()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            List<string> values = Helpers.MakeList<string>(dictionary["x"]);
            values.Sort();

            Assert.AreEqual(2, values.Count);
            Assert.AreEqual("x1", values[0]);
            Assert.AreEqual("x2", values[1]);

            values = Helpers.MakeList<string>(dictionary["y"]);

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual("y1", values[0]);

            values = Helpers.MakeList<string>(dictionary["z"]);

            Assert.AreEqual(0, values.Count);
        }

        /// <summary>
        /// Mixture of adds and removes
        /// </summary>
        [TestMethod]
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

            Assert.AreEqual(1, dictionary.KeyCount);
            Assert.AreEqual(1, dictionary.ValueCount);

            List<string> values = Helpers.MakeList<string>(dictionary["x"]);

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual("x2", values[0]);
        }

        /// <summary>
        /// Clearing out
        /// </summary>
        [TestMethod]
        public void Clear()
        {
            MultiDictionary<string, string> dictionary = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dictionary.Add("x", "x1");
            dictionary.Add("x", "x2");
            dictionary.Add("y", "y1");

            dictionary.Clear();

            Assert.AreEqual(0, dictionary.KeyCount);
            Assert.AreEqual(0, dictionary.ValueCount);
        }
    }
}