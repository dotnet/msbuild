// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit Tests for TaskParameter class, specifically focusing on 
// testing its serialization.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Microsoft.Build.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Class to specifically test the TaskParameter class, particularly its serialization 
    /// of various types of parameters.  
    /// </summary>
    [TestClass]
    public class TaskParameter_Tests
    {
        /// <summary>
        /// Verifies that construction and serialization with a null parameter is OK. 
        /// </summary>
        [TestMethod]
        public void NullParameter()
        {
            TaskParameter t = new TaskParameter(null);

            Assert.IsNull(t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.Null, t.ParameterType);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.IsNull(t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.Null, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a string parameter is OK. 
        /// </summary>
        [TestMethod]
        public void StringParameter()
        {
            TaskParameter t = new TaskParameter("foo");

            Assert.AreEqual("foo", t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.String, t.ParameterType);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual("foo", t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.String, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a string array parameter is OK. 
        /// </summary>
        [TestMethod]
        public void StringArrayParameter()
        {
            TaskParameter t = new TaskParameter(new string[] { "foo", "bar" });

            Assert.AreEqual(TaskParameterType.StringArray, t.ParameterType);

            string[] wrappedParameter = t.WrappedParameter as string[];
            Assert.IsNotNull(wrappedParameter);
            Assert.AreEqual(2, wrappedParameter.Length);
            Assert.AreEqual("foo", wrappedParameter[0]);
            Assert.AreEqual("bar", wrappedParameter[1]);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.StringArray, t2.ParameterType);

            string[] wrappedParameter2 = t2.WrappedParameter as string[];
            Assert.IsNotNull(wrappedParameter2);
            Assert.AreEqual(2, wrappedParameter2.Length);
            Assert.AreEqual("foo", wrappedParameter2[0]);
            Assert.AreEqual("bar", wrappedParameter2[1]);
        }

        /// <summary>
        /// Verifies that construction and serialization with a value type (integer) parameter is OK. 
        /// </summary>
        [TestMethod]
        public void ValueTypeParameter()
        {
            TaskParameter t = new TaskParameter(1);

            Assert.AreEqual(1, t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(1, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an array of value types (ints) is OK. 
        /// </summary>
        [TestMethod]
        public void ValueTypeArrayParameter()
        {
            TaskParameter t = new TaskParameter(new int[] { 2, 15 });

            Assert.AreEqual(TaskParameterType.ValueTypeArray, t.ParameterType);

            int[] wrappedParameter = t.WrappedParameter as int[];
            Assert.IsNotNull(wrappedParameter);
            Assert.AreEqual(2, wrappedParameter.Length);
            Assert.AreEqual(2, wrappedParameter[0]);
            Assert.AreEqual(15, wrappedParameter[1]);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ValueTypeArray, t2.ParameterType);

            int[] wrappedParameter2 = t2.WrappedParameter as int[];
            Assert.IsNotNull(wrappedParameter2);
            Assert.AreEqual(2, wrappedParameter2.Length);
            Assert.AreEqual(2, wrappedParameter2[0]);
            Assert.AreEqual(15, wrappedParameter2[1]);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter is OK. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter that has custom metadata is OK. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameterWithMetadata()
        {
            TaskItem baseItem = new TaskItem("foo");
            baseItem.SetMetadata("a", "a1");
            baseItem.SetMetadata("b", "b1");

            TaskParameter t = new TaskParameter(baseItem);

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);
            Assert.AreEqual("a1", foo.GetMetadata("a"));
            Assert.AreEqual("b1", foo.GetMetadata("b"));

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
            Assert.AreEqual("a1", foo2.GetMetadata("a"));
            Assert.AreEqual("b1", foo2.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an array of ITaskItems is OK. 
        /// </summary>
        [TestMethod]
        public void ITaskItemArrayParameter()
        {
            TaskParameter t = new TaskParameter(new ITaskItem[] { new TaskItem("foo"), new TaskItem("bar") });

            Assert.AreEqual(TaskParameterType.ITaskItemArray, t.ParameterType);

            ITaskItem[] wrappedParameter = t.WrappedParameter as ITaskItem[];
            Assert.IsNotNull(wrappedParameter);
            Assert.AreEqual(2, wrappedParameter.Length);
            Assert.AreEqual("foo", wrappedParameter[0].ItemSpec);
            Assert.AreEqual("bar", wrappedParameter[1].ItemSpec);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItemArray, t.ParameterType);

            ITaskItem[] wrappedParameter2 = t.WrappedParameter as ITaskItem[];
            Assert.IsNotNull(wrappedParameter2);
            Assert.AreEqual(2, wrappedParameter2.Length);
            Assert.AreEqual("foo", wrappedParameter2[0].ItemSpec);
            Assert.AreEqual("bar", wrappedParameter2[1].ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an 
        /// itemspec containing escapable characters translates the escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_EscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%3bbar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo;bar", foo.ItemSpec);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo;bar", foo2.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an 
        /// itemspec containing doubly-escaped characters translates the escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_DoubleEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%253bbar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo%3bbar", foo.ItemSpec);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo%3bbar", foo2.ItemSpec);

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((INodePacketTranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t4 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t4.ParameterType);

            ITaskItem foo4 = t4.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo4);
            Assert.AreEqual("foo%3bbar", foo4.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an 
        /// itemspec containing the non-escaped forms of escapable characters translates the escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_EscapableNotEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo;bar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem2 foo = t.WrappedParameter as ITaskItem2;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo;bar", foo.ItemSpec);
            Assert.AreEqual("foo;bar", foo.EvaluatedIncludeEscaped);

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem2 foo2 = t2.WrappedParameter as ITaskItem2;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo;bar", foo2.ItemSpec);
            Assert.AreEqual("foo;bar", foo2.EvaluatedIncludeEscaped);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing escapable characters translates the escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_EscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1%25b1");
            metadata.Add("b", "c1%28d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);
            Assert.AreEqual("a1%b1", foo.GetMetadata("a"));
            Assert.AreEqual("c1(d1", foo.GetMetadata("b"));

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
            Assert.AreEqual("a1%b1", foo2.GetMetadata("a"));
            Assert.AreEqual("c1(d1", foo2.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing doubly-escapabed characters translates the escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_DoubleEscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1%2525b1");
            metadata.Add("b", "c1%2528d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);
            Assert.AreEqual("a1%25b1", foo.GetMetadata("a"));
            Assert.AreEqual("c1%28d1", foo.GetMetadata("b"));

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
            Assert.AreEqual("a1%25b1", foo2.GetMetadata("a"));
            Assert.AreEqual("c1%28d1", foo2.GetMetadata("b"));

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((INodePacketTranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t4 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t4.ParameterType);

            ITaskItem foo4 = t4.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo4);
            Assert.AreEqual("foo", foo4.ItemSpec);
            Assert.AreEqual("a1%25b1", foo4.GetMetadata("a"));
            Assert.AreEqual("c1%28d1", foo4.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing the non-escaped versions of escapable characters translates the 
        /// escaping correctly. 
        /// </summary>
        [TestMethod]
        public void ITaskItemParameter_EscapableNotEscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1(b1");
            metadata.Add("b", "c1)d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem2 foo = t.WrappedParameter as ITaskItem2;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);
            Assert.AreEqual("a1(b1", foo.GetMetadata("a"));
            Assert.AreEqual("c1)d1", foo.GetMetadata("b"));
            Assert.AreEqual("a1(b1", foo.GetMetadataValueEscaped("a"));
            Assert.AreEqual("c1)d1", foo.GetMetadataValueEscaped("b"));

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem2 foo2 = t2.WrappedParameter as ITaskItem2;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
            Assert.AreEqual("a1(b1", foo2.GetMetadata("a"));
            Assert.AreEqual("c1)d1", foo2.GetMetadata("b"));
            Assert.AreEqual("a1(b1", foo2.GetMetadataValueEscaped("a"));
            Assert.AreEqual("c1)d1", foo2.GetMetadataValueEscaped("b"));
        }
    }
}