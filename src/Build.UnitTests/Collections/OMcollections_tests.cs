// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using ObjectModel = System.Collections.ObjectModel;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for several of the collections classes
    /// </summary>
    [TestClass]
    public class OMcollections_Tests
    {
        /// <summary>
        /// End to end test of PropertyDictionary
        /// </summary>
        [MSBuildTestMethod]
        public void BasicPropertyDictionary()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = GetPropertyInstance("p1", "v1");
            ProjectPropertyInstance p2 = GetPropertyInstance("p2", "v2");
            ProjectPropertyInstance p3 = GetPropertyInstance("p1", "v1");
            ProjectPropertyInstance p4 = GetPropertyInstance("p2", "v3");

            properties.Set(p1);
            properties.Set(p2);
            properties.Set(p3);
            properties.Set(p1);
            properties.Set(p4);

            Assert.AreEqual(2, properties.Count);
            Assert.AreEqual("v1", properties["p1"].EvaluatedValue);
            Assert.AreEqual("v3", properties["p2"].EvaluatedValue);

            Assert.IsTrue(properties.Remove("p1"));
            Assert.IsNull(properties["p1"]);

            Assert.IsFalse(properties.Remove("x"));

            properties.Clear();

            Assert.IsEmpty(properties);
        }

        /// <summary>
        /// Test dictionary serialization with properties
        /// </summary>
        [MSBuildTestMethod]
        public void PropertyDictionarySerialization()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = GetPropertyInstance("p1", "v1");
            ProjectPropertyInstance p2 = GetPropertyInstance("p2", "v2");
            ProjectPropertyInstance p3 = GetPropertyInstance("p1", "v1");
            ProjectPropertyInstance p4 = GetPropertyInstance("p2", "v3");

            properties.Set(p1);
            properties.Set(p2);
            properties.Set(p3);
            properties.Set(p1);
            properties.Set(p4);

            TranslationHelpers.GetWriteTranslator().TranslateDictionary<PropertyDictionary<ProjectPropertyInstance>, ProjectPropertyInstance>(ref properties, ProjectPropertyInstance.FactoryForDeserialization);
            PropertyDictionary<ProjectPropertyInstance> deserializedProperties = null;
            TranslationHelpers.GetReadTranslator().TranslateDictionary<PropertyDictionary<ProjectPropertyInstance>, ProjectPropertyInstance>(ref deserializedProperties, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.AreEqual(properties, deserializedProperties);
        }

        /// <summary>
        /// Test dictionary serialization with no properties
        /// </summary>
        [MSBuildTestMethod]
        public void PropertyDictionarySerializationEmpty()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();

            TranslationHelpers.GetWriteTranslator().TranslateDictionary<PropertyDictionary<ProjectPropertyInstance>, ProjectPropertyInstance>(ref properties, ProjectPropertyInstance.FactoryForDeserialization);
            PropertyDictionary<ProjectPropertyInstance> deserializedProperties = null;
            TranslationHelpers.GetReadTranslator().TranslateDictionary<PropertyDictionary<ProjectPropertyInstance>, ProjectPropertyInstance>(ref deserializedProperties, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.AreEqual(properties, deserializedProperties);
        }

        /// <summary>
        /// End to end test of ItemDictionary
        /// </summary>
        [MSBuildTestMethod]
        public void BasicItemDictionary()
        {
            ItemDictionary<ProjectItemInstance> items = new ItemDictionary<ProjectItemInstance>();

            // Clearing empty collection
            items.Clear();

            // Enumeration of empty collection
            using (IEnumerator<ProjectItemInstance> enumerator = items.GetEnumerator())
            {
                enumerator.MoveNext().ShouldBeFalse();
                Should.Throw<InvalidOperationException>(() =>
                {
                    object o = ((IEnumerator)enumerator).Current;
                });
                enumerator.Current.ShouldBeNull();
            }

            List<ProjectItemInstance> list = new List<ProjectItemInstance>();
            foreach (ProjectItemInstance item in items)
            {
                list.Add(item);
            }

            Assert.IsEmpty(list);

            // Cause an empty list for type 'x' to be added
            ICollection<ProjectItemInstance> itemList = items["x"];

            // Enumerate empty collection, with an empty list in it
            foreach (ProjectItemInstance item in items)
            {
                list.Add(item);
            }

            Assert.IsEmpty(list);

            // Add and remove some items
            ProjectItemInstance item1 = GetItemInstance("i", "i1");
            Assert.IsFalse(items.Remove(item1));
            Assert.IsEmpty(items["j"]);

            items.Add(item1);
            Assert.ContainsSingle(items["i"]);
            Assert.AreEqual(item1, items["i"].First());

            ProjectItemInstance item2 = GetItemInstance("i", "i2");
            items.Add(item2);
            ProjectItemInstance item3 = GetItemInstance("j", "j1");
            items.Add(item3);

            // Enumerate to verify contents
            list = new List<ProjectItemInstance>();
            foreach (ProjectItemInstance item in items)
            {
                list.Add(item);
            }

            list.Sort(ProjectItemInstanceComparer);
            Assert.AreEqual(item1, list[0]);
            Assert.AreEqual(item2, list[1]);
            Assert.AreEqual(item3, list[2]);

            // Direct operations on the enumerator
            using (IEnumerator<ProjectItemInstance> enumerator = items.GetEnumerator())
            {
                Assert.IsNull(enumerator.Current);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsNotNull(enumerator.Current);
                enumerator.Reset();
                Assert.IsNull(enumerator.Current);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsNotNull(enumerator.Current);
            }
        }

        /// <summary>
        /// Null backing collection should be like empty collection
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyDictionaryNullBackingClone()
        {
            var dictionary = CreateCloneDictionary<string>(null, StringComparer.OrdinalIgnoreCase);
            Assert.IsEmpty(dictionary);
        }

        /// <summary>
        /// Null backing collection should be like empty collection
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyDictionaryNullBackingWrapper()
        {
            var dictionary = new ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));
            Assert.IsEmpty(dictionary);
        }

        /// <summary>
        /// Cloning constructor should not see subsequent changes
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyDictionaryClone()
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary.Add("p", "v");

            var readOnlyDictionary = CreateCloneDictionary(dictionary, StringComparer.OrdinalIgnoreCase);
            dictionary.Add("p2", "v2");

            Assert.ContainsSingle(readOnlyDictionary);
            Assert.IsTrue(readOnlyDictionary.ContainsKey("P"));
            Assert.IsFalse(readOnlyDictionary.ContainsKey("p2"));
        }

        /// <summary>
        /// Wrapping constructor should be "live"
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyDictionaryWrapper()
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary.Add("p", "v");

            var readOnlyDictionary = new ObjectModel.ReadOnlyDictionary<string, string>(dictionary);
            dictionary.Add("p2", "v2");

            Assert.AreEqual(2, dictionary.Count);
            Assert.IsTrue(dictionary.ContainsKey("p2"));
        }

        /// <summary>
        /// Null backing collection should be an error
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyCollectionNullBacking()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                new ReadOnlyCollection<string>(null);
            });
        }
        /// <summary>
        /// Verify non generic enumeration does not recurse
        /// ie., GetEnumerator() does not call itself
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyDictionaryNonGenericEnumeration()
        {
            var backing = new Dictionary<string, string>();
            var collection = new ObjectModel.ReadOnlyDictionary<string, string>(backing);
            IEnumerable enumerable = (IEnumerable)collection;

            // Does not overflow stack:
            foreach (object o in enumerable)
            {
            }
        }

        /// <summary>
        /// Verify that the converting dictionary functions.
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyConvertingDictionary()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            values["one"] = "1";
            values["two"] = "2";
            values["three"] = "3";

            Dictionary<string, int> convertedValues = new Dictionary<string, int>();
            convertedValues["one"] = 1;
            convertedValues["two"] = 2;
            convertedValues["three"] = 3;

            ReadOnlyConvertingDictionary<string, string, int> convertingCollection = new ReadOnlyConvertingDictionary<string, string, int>(values, delegate (string x) { return Convert.ToInt32(x); });
            Assert.AreEqual(3, convertingCollection.Count);
            Assert.IsTrue(convertingCollection.IsReadOnly);

            foreach (KeyValuePair<string, int> value in convertingCollection)
            {
                Assert.AreEqual(convertedValues[value.Key], value.Value);
            }
        }

        /// <summary>
        /// Verify non generic enumeration does not recurse
        /// ie., GetEnumerator() does not call itself
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOnlyCollectionNonGenericEnumeration()
        {
            var backing = new List<string>();
            var collection = new ReadOnlyCollection<string>(backing);
            IEnumerable enumerable = (IEnumerable)collection;

            // Does not overflow stack:
            foreach (object o in enumerable)
            {
            }
        }

        /// <summary>
        /// Helper to make a ProjectPropertyInstance.
        /// </summary>
        private static ProjectPropertyInstance GetPropertyInstance(string name, string value)
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectPropertyInstance property = projectInstance.SetProperty(name, value);

            return property;
        }

        /// <summary>
        /// Helper to make a ProjectItemInstance.
        /// </summary>
        private static ProjectItemInstance GetItemInstance(string itemType, string evaluatedInclude)
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectItemInstance item = projectInstance.AddItem(itemType, evaluatedInclude);

            return item;
        }

        /// <summary>
        /// Creates a copy of a dictionary and returns a read-only dictionary around the results.
        /// </summary>
        /// <typeparam name="TValue">The value stored in the dictionary</typeparam>
        /// <param name="dictionary">Dictionary to clone.</param>
        private static ObjectModel.ReadOnlyDictionary<string, TValue> CreateCloneDictionary<TValue>(IDictionary<string, TValue> dictionary, StringComparer strComparer)
        {
            Dictionary<string, TValue> clone;
            if (dictionary == null)
            {
                clone = new Dictionary<string, TValue>(0);
            }
            else
            {
                clone = new Dictionary<string, TValue>(dictionary, strComparer);
            }

            return new ObjectModel.ReadOnlyDictionary<string, TValue>(clone);
        }

        /// <summary>
        /// Simple comparer for ProjectItemInstances. Ought to compare metadata etc.
        /// </summary>
        private int ProjectItemInstanceComparer(ProjectItemInstance one, ProjectItemInstance two)
        {
            return String.Compare(one.EvaluatedInclude, two.EvaluatedInclude, StringComparison.Ordinal);
        }
    }
}
