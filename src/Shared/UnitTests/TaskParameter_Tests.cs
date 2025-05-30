// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

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

        [Theory]
        [InlineData(typeof(bool), (int)TypeCode.Boolean, "True")]
        [InlineData(typeof(byte), (int)TypeCode.Byte, "127")]
        [InlineData(typeof(sbyte), (int)TypeCode.SByte, "-127")]
        [InlineData(typeof(double), (int)TypeCode.Double, "3.14")]
        [InlineData(typeof(float), (int)TypeCode.Single, "3.14")]
        [InlineData(typeof(short), (int)TypeCode.Int16, "-20000")]
        [InlineData(typeof(ushort), (int)TypeCode.UInt16, "30000")]
        [InlineData(typeof(int), (int)TypeCode.Int32, "-1")]
        [InlineData(typeof(uint), (int)TypeCode.UInt32, "1")]
        [InlineData(typeof(long), (int)TypeCode.Int64, "-1000000000000")]
        [InlineData(typeof(ulong), (int)TypeCode.UInt64, "1000000000000")]
        [InlineData(typeof(decimal), (int)TypeCode.Decimal, "29.99")]
        [InlineData(typeof(char), (int)TypeCode.Char, "q")]
        [InlineData(typeof(string), (int)TypeCode.String, "foo")]
        [InlineData(typeof(DateTime), (int)TypeCode.DateTime, "1/1/2000 12:12:12")]
        public void PrimitiveParameter(Type type, int expectedTypeCodeAsInt, string testValueAsString)
        {
            TypeCode expectedTypeCode = (TypeCode)expectedTypeCodeAsInt;

            object value = Convert.ChangeType(testValueAsString, type, CultureInfo.InvariantCulture);
            TaskParameter t = new TaskParameter(value);

            Assert.Equal(value, t.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveType, t.ParameterType);
            Assert.Equal(expectedTypeCode, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(value, t2.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveType, t2.ParameterType);
            Assert.Equal(expectedTypeCode, t2.ParameterTypeCode);
        }

        [Theory]
        [InlineData(typeof(bool), (int)TypeCode.Boolean, "True;False;True")]
        [InlineData(typeof(byte), (int)TypeCode.Byte, "127;100;0")]
        [InlineData(typeof(sbyte), (int)TypeCode.SByte, "-127;-126;12")]
        [InlineData(typeof(double), (int)TypeCode.Double, "3.14;3.15")]
        [InlineData(typeof(float), (int)TypeCode.Single, "3.14;3.15")]
        [InlineData(typeof(short), (int)TypeCode.Int16, "-20000;0;-1")]
        [InlineData(typeof(ushort), (int)TypeCode.UInt16, "30000;20000;10")]
        [InlineData(typeof(int), (int)TypeCode.Int32, "-1;-2")]
        [InlineData(typeof(uint), (int)TypeCode.UInt32, "1;5;6")]
        [InlineData(typeof(long), (int)TypeCode.Int64, "-1000000000000;0")]
        [InlineData(typeof(ulong), (int)TypeCode.UInt64, "1000000000000;0")]
        [InlineData(typeof(decimal), (int)TypeCode.Decimal, "29.99;0.88")]
        [InlineData(typeof(char), (int)TypeCode.Char, "q;r;c")]
        [InlineData(typeof(string), (int)TypeCode.String, "foo;bar")]
        [InlineData(typeof(DateTime), (int)TypeCode.DateTime, "1/1/2000 12:12:12;2/2/2000 13:13:13")]
        public void PrimitiveArrayParameter(Type type, int expectedTypeCodeAsInt, string testValueAsString)
        {
            TypeCode expectedTypeCode = (TypeCode)expectedTypeCodeAsInt;

            string[] values = testValueAsString.Split(';');
            Array array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                object value = Convert.ChangeType(values[i], type, CultureInfo.InvariantCulture);
                array.SetValue(value, i);
            }

            TaskParameter t = new TaskParameter(array);

            Assert.Equal(array, t.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveTypeArray, t.ParameterType);
            Assert.Equal(expectedTypeCode, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(array, t2.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveTypeArray, t2.ParameterType);
            Assert.Equal(expectedTypeCode, t2.ParameterTypeCode);
        }

        [Fact]
        public void ValueTypeParameter()
        {
            TaskBuilderTestTask.CustomStruct value = new TaskBuilderTestTask.CustomStruct(3.14);
            TaskParameter t = new TaskParameter(value);

            Assert.Equal(value, t.WrappedParameter);
            Assert.Equal(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Custom IConvertible structs are deserialized into strings.
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), t2.WrappedParameter);
            Assert.Equal(TaskParameterType.ValueType, t2.ParameterType);
        }

        [Fact]
        public void ValueTypeArrayParameter()
        {
            TaskBuilderTestTask.CustomStruct[] value = new TaskBuilderTestTask.CustomStruct[]
            {
                new TaskBuilderTestTask.CustomStruct(3.14),
                new TaskBuilderTestTask.CustomStruct(2.72),
            };
            TaskParameter t = new TaskParameter(value);

            Assert.Equal(value, t.WrappedParameter);
            Assert.Equal(TaskParameterType.ValueTypeArray, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Custom IConvertible structs are deserialized into strings.
            Assert.True(t2.WrappedParameter is string[]);
            Assert.Equal(TaskParameterType.ValueTypeArray, t2.ParameterType);

            string[] stringArray = (string[])t2.WrappedParameter;
            Assert.Equal(2, stringArray.Length);
            Assert.Equal(value[0].ToString(CultureInfo.InvariantCulture), stringArray[0]);
            Assert.Equal(value[1].ToString(CultureInfo.InvariantCulture), stringArray[1]);
        }

        private enum TestEnumForParameter
        {
            Something,
            SomethingElse
        }

        [Fact]
        public void EnumParameter()
        {
            TaskParameter t = new TaskParameter(TestEnumForParameter.SomethingElse);

            Assert.Equal("SomethingElse", t.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveType, t.ParameterType);
            Assert.Equal(TypeCode.String, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal("SomethingElse", t2.WrappedParameter);
            Assert.Equal(TaskParameterType.PrimitiveType, t2.ParameterType);
            Assert.Equal(TypeCode.String, t2.ParameterTypeCode);
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
