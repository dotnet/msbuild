// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Class to specifically test the TaskParameter class, particularly its serialization
    /// of various types of parameters.
    /// </summary>
    public class TaskParameter_Tests
    {
        /// <summary>
        /// Verifies that construction and serialization with a null parameter is OK.
        /// </summary>
        [Fact]
        public void NullParameter()
        {
            TaskParameter t = new TaskParameter(null);

            Assert.Null(t.WrappedParameter);
            Assert.Equal(TaskParameterType.Null, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Null(t2.WrappedParameter);
            Assert.Equal(TaskParameterType.Null, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a string parameter is OK.
        /// </summary>
        [Fact]
        public void StringParameter()
        {
            TaskParameter t = new TaskParameter("foo");

            Assert.Equal("foo", t.WrappedParameter);
            Assert.Equal(TaskParameterType.String, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal("foo", t2.WrappedParameter);
            Assert.Equal(TaskParameterType.String, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a string array parameter is OK.
        /// </summary>
        [Fact]
        public void StringArrayParameter()
        {
            TaskParameter t = new TaskParameter(new string[] { "foo", "bar" });

            Assert.Equal(TaskParameterType.StringArray, t.ParameterType);

            string[] wrappedParameter = t.WrappedParameter as string[];
            Assert.NotNull(wrappedParameter);
            Assert.Equal(2, wrappedParameter.Length);
            Assert.Equal("foo", wrappedParameter[0]);
            Assert.Equal("bar", wrappedParameter[1]);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.StringArray, t2.ParameterType);

            string[] wrappedParameter2 = t2.WrappedParameter as string[];
            Assert.NotNull(wrappedParameter2);
            Assert.Equal(2, wrappedParameter2.Length);
            Assert.Equal("foo", wrappedParameter2[0]);
            Assert.Equal("bar", wrappedParameter2[1]);
        }

        /// <summary>
        /// Verifies that construction and serialization with a value type (integer) parameter is OK.
        /// </summary>
        [Fact]
        public void ValueTypeParameter()
        {
            TaskParameter t = new TaskParameter(1);

            Assert.Equal(1, t.WrappedParameter);
            Assert.Equal(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(1, t2.WrappedParameter);
            Assert.Equal(TaskParameterType.ValueType, t2.ParameterType);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an array of value types (ints) is OK.
        /// </summary>
        [Fact]
        public void ValueTypeArrayParameter()
        {
            TaskParameter t = new TaskParameter(new int[] { 2, 15 });

            Assert.Equal(TaskParameterType.ValueTypeArray, t.ParameterType);

            int[] wrappedParameter = t.WrappedParameter as int[];
            Assert.NotNull(wrappedParameter);
            Assert.Equal(2, wrappedParameter.Length);
            Assert.Equal(2, wrappedParameter[0]);
            Assert.Equal(15, wrappedParameter[1]);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ValueTypeArray, t2.ParameterType);

            int[] wrappedParameter2 = t2.WrappedParameter as int[];
            Assert.NotNull(wrappedParameter2);
            Assert.Equal(2, wrappedParameter2.Length);
            Assert.Equal(2, wrappedParameter2[0]);
            Assert.Equal(15, wrappedParameter2[1]);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter is OK.
        /// </summary>
        [Fact]
        public void ITaskItemParameter()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo"));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo", foo2.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter that has custom metadata is OK.
        /// </summary>
        [Fact]
        public void ITaskItemParameterWithMetadata()
        {
            TaskItem baseItem = new TaskItem("foo");
            baseItem.SetMetadata("a", "a1");
            baseItem.SetMetadata("b", "b1");

            TaskParameter t = new TaskParameter(baseItem);

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo", foo.ItemSpec);
            Assert.Equal("a1", foo.GetMetadata("a"));
            Assert.Equal("b1", foo.GetMetadata("b"));

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo", foo2.ItemSpec);
            Assert.Equal("a1", foo2.GetMetadata("a"));
            Assert.Equal("b1", foo2.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an array of ITaskItems is OK.
        /// </summary>
        [Fact]
        public void ITaskItemArrayParameter()
        {
            TaskParameter t = new TaskParameter(new ITaskItem[] { new TaskItem("foo"), new TaskItem("bar") });

            Assert.Equal(TaskParameterType.ITaskItemArray, t.ParameterType);

            ITaskItem[] wrappedParameter = t.WrappedParameter as ITaskItem[];
            Assert.NotNull(wrappedParameter);
            Assert.Equal(2, wrappedParameter.Length);
            Assert.Equal("foo", wrappedParameter[0].ItemSpec);
            Assert.Equal("bar", wrappedParameter[1].ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItemArray, t.ParameterType);

            ITaskItem[] wrappedParameter2 = t.WrappedParameter as ITaskItem[];
            Assert.NotNull(wrappedParameter2);
            Assert.Equal(2, wrappedParameter2.Length);
            Assert.Equal("foo", wrappedParameter2[0].ItemSpec);
            Assert.Equal("bar", wrappedParameter2[1].ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an
        /// itemspec containing escapable characters translates the escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_EscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%3bbar"));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo;bar", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo;bar", foo2.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an
        /// itemspec containing doubly-escaped characters translates the escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_DoubleEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%253bbar"));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo%3bbar", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo%3bbar", foo2.ItemSpec);

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((ITranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t4 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t4.ParameterType);

            ITaskItem foo4 = t4.WrappedParameter as ITaskItem;
            Assert.NotNull(foo4);
            Assert.Equal("foo%3bbar", foo4.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with an
        /// itemspec containing the non-escaped forms of escapable characters translates the escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_EscapableNotEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo;bar"));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem2 foo = t.WrappedParameter as ITaskItem2;
            Assert.NotNull(foo);
            Assert.Equal("foo;bar", foo.ItemSpec);
            Assert.Equal("foo;bar", foo.EvaluatedIncludeEscaped);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem2 foo2 = t2.WrappedParameter as ITaskItem2;
            Assert.NotNull(foo2);
            Assert.Equal("foo;bar", foo2.ItemSpec);
            Assert.Equal("foo;bar", foo2.EvaluatedIncludeEscaped);
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing escapable characters translates the escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_EscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1%25b1");
            metadata.Add("b", "c1%28d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo", foo.ItemSpec);
            Assert.Equal("a1%b1", foo.GetMetadata("a"));
            Assert.Equal("c1(d1", foo.GetMetadata("b"));

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo", foo2.ItemSpec);
            Assert.Equal("a1%b1", foo2.GetMetadata("a"));
            Assert.Equal("c1(d1", foo2.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing doubly-escaped characters translates the escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_DoubleEscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1%2525b1");
            metadata.Add("b", "c1%2528d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.NotNull(foo);
            Assert.Equal("foo", foo.ItemSpec);
            Assert.Equal("a1%25b1", foo.GetMetadata("a"));
            Assert.Equal("c1%28d1", foo.GetMetadata("b"));

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.NotNull(foo2);
            Assert.Equal("foo", foo2.ItemSpec);
            Assert.Equal("a1%25b1", foo2.GetMetadata("a"));
            Assert.Equal("c1%28d1", foo2.GetMetadata("b"));

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((ITranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t4 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t4.ParameterType);

            ITaskItem foo4 = t4.WrappedParameter as ITaskItem;
            Assert.NotNull(foo4);
            Assert.Equal("foo", foo4.ItemSpec);
            Assert.Equal("a1%25b1", foo4.GetMetadata("a"));
            Assert.Equal("c1%28d1", foo4.GetMetadata("b"));
        }

        /// <summary>
        /// Verifies that construction and serialization with a parameter that is an ITaskItem with
        /// metadata containing the non-escaped versions of escapable characters translates the
        /// escaping correctly.
        /// </summary>
        [Fact]
        public void ITaskItemParameter_EscapableNotEscapedMetadata()
        {
            IDictionary metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata.Add("a", "a1(b1");
            metadata.Add("b", "c1)d1");

            TaskParameter t = new TaskParameter(new TaskItem("foo", metadata));

            Assert.Equal(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem2 foo = t.WrappedParameter as ITaskItem2;
            Assert.NotNull(foo);
            Assert.Equal("foo", foo.ItemSpec);
            Assert.Equal("a1(b1", foo.GetMetadata("a"));
            Assert.Equal("c1)d1", foo.GetMetadata("b"));
            Assert.Equal("a1(b1", foo.GetMetadataValueEscaped("a"));
            Assert.Equal("c1)d1", foo.GetMetadataValueEscaped("b"));

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem2 foo2 = t2.WrappedParameter as ITaskItem2;
            Assert.NotNull(foo2);
            Assert.Equal("foo", foo2.ItemSpec);
            Assert.Equal("a1(b1", foo2.GetMetadata("a"));
            Assert.Equal("c1)d1", foo2.GetMetadata("b"));
            Assert.Equal("a1(b1", foo2.GetMetadataValueEscaped("a"));
            Assert.Equal("c1)d1", foo2.GetMetadataValueEscaped("b"));
        }
    }
}
