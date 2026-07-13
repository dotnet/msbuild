// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Microsoft.Build.Utilities;
using Shouldly;

using ProjectItemInstanceTaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

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
        [MSBuildTestMethod]
        public void NullParameter()
        {
            TaskParameter t = new TaskParameter(null);

            Assert.IsNull(t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.Null, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.IsNull(t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.Null, t2.ParameterType);
        }

        [MSBuildTestMethod]
        [DataRow(typeof(bool), (int)TypeCode.Boolean, "True")]
        [DataRow(typeof(byte), (int)TypeCode.Byte, "127")]
        [DataRow(typeof(sbyte), (int)TypeCode.SByte, "-127")]
        [DataRow(typeof(double), (int)TypeCode.Double, "3.14")]
        [DataRow(typeof(float), (int)TypeCode.Single, "3.14")]
        [DataRow(typeof(short), (int)TypeCode.Int16, "-20000")]
        [DataRow(typeof(ushort), (int)TypeCode.UInt16, "30000")]
        [DataRow(typeof(int), (int)TypeCode.Int32, "-1")]
        [DataRow(typeof(uint), (int)TypeCode.UInt32, "1")]
        [DataRow(typeof(long), (int)TypeCode.Int64, "-1000000000000")]
        [DataRow(typeof(ulong), (int)TypeCode.UInt64, "1000000000000")]
        [DataRow(typeof(decimal), (int)TypeCode.Decimal, "29.99")]
        [DataRow(typeof(char), (int)TypeCode.Char, "q")]
        [DataRow(typeof(string), (int)TypeCode.String, "foo")]
        [DataRow(typeof(DateTime), (int)TypeCode.DateTime, "1/1/2000 12:12:12")]
        public void PrimitiveParameter(Type type, int expectedTypeCodeAsInt, string testValueAsString)
        {
            TypeCode expectedTypeCode = (TypeCode)expectedTypeCodeAsInt;

            object value = Convert.ChangeType(testValueAsString, type, CultureInfo.InvariantCulture);
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(value, t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveType, t.ParameterType);
            Assert.AreEqual(expectedTypeCode, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(value, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveType, t2.ParameterType);
            Assert.AreEqual(expectedTypeCode, t2.ParameterTypeCode);
        }

        [MSBuildTestMethod]
        [DataRow(typeof(bool), (int)TypeCode.Boolean, "True;False;True")]
        [DataRow(typeof(byte), (int)TypeCode.Byte, "127;100;0")]
        [DataRow(typeof(sbyte), (int)TypeCode.SByte, "-127;-126;12")]
        [DataRow(typeof(double), (int)TypeCode.Double, "3.14;3.15")]
        [DataRow(typeof(float), (int)TypeCode.Single, "3.14;3.15")]
        [DataRow(typeof(short), (int)TypeCode.Int16, "-20000;0;-1")]
        [DataRow(typeof(ushort), (int)TypeCode.UInt16, "30000;20000;10")]
        [DataRow(typeof(int), (int)TypeCode.Int32, "-1;-2")]
        [DataRow(typeof(uint), (int)TypeCode.UInt32, "1;5;6")]
        [DataRow(typeof(long), (int)TypeCode.Int64, "-1000000000000;0")]
        [DataRow(typeof(ulong), (int)TypeCode.UInt64, "1000000000000;0")]
        [DataRow(typeof(decimal), (int)TypeCode.Decimal, "29.99;0.88")]
        [DataRow(typeof(char), (int)TypeCode.Char, "q;r;c")]
        [DataRow(typeof(string), (int)TypeCode.String, "foo;bar")]
        [DataRow(typeof(DateTime), (int)TypeCode.DateTime, "1/1/2000 12:12:12;2/2/2000 13:13:13")]
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

            Assert.AreEqual(array, t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveTypeArray, t.ParameterType);
            Assert.AreEqual(expectedTypeCode, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(array, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveTypeArray, t2.ParameterType);
            Assert.AreEqual(expectedTypeCode, t2.ParameterTypeCode);
        }

        [MSBuildTestMethod]
        public void ValueTypeParameter()
        {
            TaskBuilderTestTask.CustomStruct value = new TaskBuilderTestTask.CustomStruct(3.14);
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(value, t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Custom IConvertible structs are deserialized into strings.
            Assert.AreEqual(value.ToString(CultureInfo.InvariantCulture), t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        [MSBuildTestMethod]
        public void ValueTypeArrayParameter()
        {
            TaskBuilderTestTask.CustomStruct[] value = new TaskBuilderTestTask.CustomStruct[]
            {
                new TaskBuilderTestTask.CustomStruct(3.14),
                new TaskBuilderTestTask.CustomStruct(2.72),
            };
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(value, t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueTypeArray, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Custom IConvertible structs are deserialized into strings.
            Assert.IsTrue(t2.WrappedParameter is string[]);
            Assert.AreEqual(TaskParameterType.ValueTypeArray, t2.ParameterType);

            string[] stringArray = (string[])t2.WrappedParameter;
            Assert.AreEqual(2, stringArray.Length);
            Assert.AreEqual(value[0].ToString(CultureInfo.InvariantCulture), stringArray[0]);
            Assert.AreEqual(value[1].ToString(CultureInfo.InvariantCulture), stringArray[1]);
        }

        /// <summary>
        /// A default <see cref="AbsolutePath"/> (null Value) must serialize cleanly. This is the
        /// scenario that crashed the out-of-proc task host: a struct-typed output is never null, so
        /// it always gets serialized, and the old code threw InvalidCastException because AbsolutePath
        /// is not IConvertible. See https://github.com/dotnet/msbuild/pull/13016.
        /// </summary>
        [MSBuildTestMethod]
        public void DefaultAbsolutePathParameter()
        {
            TaskParameter t = new TaskParameter(default(AbsolutePath));

            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // A default AbsolutePath has a null Value and serializes to the empty string.
            Assert.AreEqual(string.Empty, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        [MSBuildTestMethod]
        public void AbsolutePathParameter()
        {
            string path = Path.Combine(Path.GetTempPath(), "TaskParameterTests_file.txt");
            AbsolutePath value = new AbsolutePath(path);
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // Path types are serialized using the same canonical conversion as the in-process engine.
            Assert.AreEqual(value.Value, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        [MSBuildTestMethod]
        public void AbsolutePathArrayParameter()
        {
            AbsolutePath[] value = new AbsolutePath[]
            {
                new AbsolutePath(Path.Combine(Path.GetTempPath(), "a.txt")),
                new AbsolutePath(Path.Combine(Path.GetTempPath(), "b.txt")),
            };
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueTypeArray, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            string[] stringArray = Assert.IsExactInstanceOfType<string[]>(t2.WrappedParameter);
            Assert.AreEqual(value[0].Value, stringArray[0]);
            Assert.AreEqual(value[1].Value, stringArray[1]);
        }

        [MSBuildTestMethod]
        public void FileInfoParameter()
        {
            FileInfo value = new FileInfo(Path.Combine(Path.GetTempPath(), "TaskParameterTests_file.txt"));
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            // FileInfo is serialized as its full path, matching ValueTypeParser/the in-process engine.
            Assert.AreEqual(value.FullName, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        [MSBuildTestMethod]
        public void DirectoryInfoParameter()
        {
            DirectoryInfo value = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "TaskParameterTests_dir"));
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueType, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(value.FullName, t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.ValueType, t2.ParameterType);
        }

        [MSBuildTestMethod]
        public void FileInfoArrayParameter()
        {
            FileInfo[] value = new FileInfo[]
            {
                new FileInfo(Path.Combine(Path.GetTempPath(), "a.txt")),
                new FileInfo(Path.Combine(Path.GetTempPath(), "b.txt")),
            };
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueTypeArray, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            string[] stringArray = Assert.IsExactInstanceOfType<string[]>(t2.WrappedParameter);
            Assert.AreEqual(value[0].FullName, stringArray[0]);
            Assert.AreEqual(value[1].FullName, stringArray[1]);
        }

        [MSBuildTestMethod]
        public void DirectoryInfoArrayParameter()
        {
            DirectoryInfo[] value = new DirectoryInfo[]
            {
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), "dirA")),
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), "dirB")),
            };
            TaskParameter t = new TaskParameter(value);

            Assert.AreEqual(TaskParameterType.ValueTypeArray, t.ParameterType);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            string[] stringArray = Assert.IsExactInstanceOfType<string[]>(t2.WrappedParameter);
            Assert.AreEqual(value[0].FullName, stringArray[0]);
            Assert.AreEqual(value[1].FullName, stringArray[1]);
        }

        private enum TestEnumForParameter
        {
            Something,
            SomethingElse
        }

        [MSBuildTestMethod]
        public void EnumParameter()
        {
            TaskParameter t = new TaskParameter(TestEnumForParameter.SomethingElse);

            Assert.AreEqual("SomethingElse", t.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveType, t.ParameterType);
            Assert.AreEqual(TypeCode.String, t.ParameterTypeCode);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual("SomethingElse", t2.WrappedParameter);
            Assert.AreEqual(TaskParameterType.PrimitiveType, t2.ParameterType);
            Assert.AreEqual(TypeCode.String, t2.ParameterTypeCode);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter is OK.
        /// </summary>
        [MSBuildTestMethod]
        public void ITaskItemParameter()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
        }

        /// <summary>
        /// Verifies that construction and serialization with an ITaskItem parameter that has custom metadata is OK.
        /// </summary>
        [MSBuildTestMethod]
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

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
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
        [MSBuildTestMethod]
        public void ITaskItemArrayParameter()
        {
            TaskParameter t = new TaskParameter(new ITaskItem[] { new TaskItem("foo"), new TaskItem("bar") });

            Assert.AreEqual(TaskParameterType.ITaskItemArray, t.ParameterType);

            ITaskItem[] wrappedParameter = t.WrappedParameter as ITaskItem[];
            Assert.IsNotNull(wrappedParameter);
            Assert.AreEqual(2, wrappedParameter.Length);
            Assert.AreEqual("foo", wrappedParameter[0].ItemSpec);
            Assert.AreEqual("bar", wrappedParameter[1].ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

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
        [MSBuildTestMethod]
        public void ITaskItemParameter_EscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%3bbar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo;bar", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
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
        [MSBuildTestMethod]
        public void ITaskItemParameter_DoubleEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo%253bbar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem foo = t.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo%3bbar", foo.ItemSpec);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo%3bbar", foo2.ItemSpec);

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((ITranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
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
        [MSBuildTestMethod]
        public void ITaskItemParameter_EscapableNotEscapedItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("foo;bar"));

            Assert.AreEqual(TaskParameterType.ITaskItem, t.ParameterType);

            ITaskItem2 foo = t.WrappedParameter as ITaskItem2;
            Assert.IsNotNull(foo);
            Assert.AreEqual("foo;bar", foo.ItemSpec);
            Assert.AreEqual("foo;bar", foo.EvaluatedIncludeEscaped);

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
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
        [MSBuildTestMethod]
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

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
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
        /// metadata containing doubly-escaped characters translates the escaping correctly.
        /// </summary>
        [MSBuildTestMethod]
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

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.AreEqual(TaskParameterType.ITaskItem, t2.ParameterType);

            ITaskItem foo2 = t2.WrappedParameter as ITaskItem;
            Assert.IsNotNull(foo2);
            Assert.AreEqual("foo", foo2.ItemSpec);
            Assert.AreEqual("a1%25b1", foo2.GetMetadata("a"));
            Assert.AreEqual("c1%28d1", foo2.GetMetadata("b"));

            TaskParameter t3 = new TaskParameter(t2.WrappedParameter);

            ((ITranslatable)t3).Translate(TranslationHelpers.GetWriteTranslator());
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
        [MSBuildTestMethod]
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

            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
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

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13140
        /// RecursiveDir (built-in, non-derivable metadata) must survive TaskParameter
        /// serialization, which is the TaskHost boundary crossed in -mt mode.
        /// </summary>
        [MSBuildTestMethod]
        public void RecursiveDirSurvivesTaskParameterSerialization()
        {
            string sep = Path.DirectorySeparatorChar.ToString();
            string itemSpec = $"src{sep}sub1{sep}sub2{sep}file.txt";
            string wildcardPattern = $"src{sep}**{sep}file.txt";
            string expectedRecursiveDir = $"sub1{sep}sub2{sep}";

            // ProjectItemInstance.TaskItem computes RecursiveDir as built-in metadata
            // from the wildcard pattern — it is NOT in CloneCustomMetadataEscaped().
            var item = new ProjectItemInstanceTaskItem(
                includeEscaped: itemSpec,
                includeBeforeWildcardExpansionEscaped: wildcardPattern,
                directMetadata: null,
                itemDefinitions: null,
                projectDirectory: Directory.GetCurrentDirectory(),
                immutable: false,
                definingFileEscaped: "test.proj");

            item.GetMetadata("RecursiveDir").ShouldBe(expectedRecursiveDir);
            ((ITaskItem2)item).CloneCustomMetadataEscaped().Contains("RecursiveDir").ShouldBeFalse();

            // Wrap in TaskParameter (TaskHost serialization boundary)
            TaskParameter t = new TaskParameter(item);
            (t.WrappedParameter as ITaskItem).GetMetadata("RecursiveDir").ShouldBe(expectedRecursiveDir);

            // Round-trip serialize/deserialize
            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            (t2.WrappedParameter as ITaskItem).GetMetadata("RecursiveDir").ShouldBe(expectedRecursiveDir);
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13896
        /// ToString() on the marshaled task item must return the item-spec (matching every other
        /// ITaskItem implementation), not the .NET type name. In -mt mode (and out-of-proc TaskHost)
        /// task outputs come back as TaskParameterTaskItem; tasks that consume them may call ToString()
        /// expecting the spec. Without the override they would get
        /// "Microsoft.Build.BackEnd.TaskParameter+TaskParameterTaskItem" instead.
        /// </summary>
        [MSBuildTestMethod]
        public void ITaskItemParameter_ToStringReturnsItemSpec()
        {
            TaskParameter t = new TaskParameter(new TaskItem("some/path/foo.dll"));

            ITaskItem wrapped = t.WrappedParameter as ITaskItem;
            wrapped.ShouldNotBeNull();
            wrapped.ToString().ShouldBe("some/path/foo.dll");

            // The spec is returned in its escaped form, matching ProjectItemInstance.TaskItem.ToString().
            ITaskItem wrappedEscaped = new TaskParameter(new TaskItem("foo%3bbar")).WrappedParameter as ITaskItem;
            wrappedEscaped.ShouldNotBeNull();
            wrappedEscaped.ToString().ShouldBe("foo%3bbar");
            wrappedEscaped.ItemSpec.ShouldBe("foo;bar");

            // The spec survives serialization (the TaskHost / -mt marshaling boundary).
            ((ITranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            TaskParameter t2 = TaskParameter.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            ITaskItem wrapped2 = t2.WrappedParameter as ITaskItem;
            wrapped2.ShouldNotBeNull();
            wrapped2.ToString().ShouldBe("some/path/foo.dll");
        }

#if FEATURE_APPDOMAIN
        private sealed class RemoteTaskItemFactory : MarshalByRefObject
        {
            public TaskItem CreateTaskItem() => new TaskItem();

            public ITaskItem CreateTaskParameterTaskItem()
            {
                TaskParameter t = new TaskParameter(new TaskItem());
                return t.WrappedParameter as ITaskItem;
            }
        }

        [MSBuildTestMethod]
        public void ITaskItemParameter_CopyMetadataToRemoteTaskItem()
        {
            TaskItem sourceItem = new TaskItem();
            sourceItem.SetMetadata("a", "a1");
            sourceItem.SetMetadata("b", "b1");
            TaskParameter t = new TaskParameter(sourceItem);
            ITaskItem fromItem = t.WrappedParameter as ITaskItem;

            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain("CopyMetadataToRemoteTaskItem", null, AppDomain.CurrentDomain.SetupInformation);
                RemoteTaskItemFactory itemFactory = (RemoteTaskItemFactory)appDomain.CreateInstanceFromAndUnwrap(typeof(RemoteTaskItemFactory).Module.FullyQualifiedName, typeof(RemoteTaskItemFactory).FullName);

                TaskItem toItem = itemFactory.CreateTaskItem();

                fromItem.CopyMetadataTo(toItem);

                Assert.AreEqual("a1", toItem.GetMetadata("a"));
                Assert.AreEqual("b1", toItem.GetMetadata("b"));
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        [MSBuildTestMethod]
        public void ITaskItemParameter_CopyMetadataFromRemoteTaskItem()
        {
            TaskItem toItem = new TaskItem();

            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain("CopyMetadataFromRemoteTaskItem", null, AppDomain.CurrentDomain.SetupInformation);
                RemoteTaskItemFactory itemFactory = (RemoteTaskItemFactory)appDomain.CreateInstanceFromAndUnwrap(typeof(RemoteTaskItemFactory).Module.FullyQualifiedName, typeof(RemoteTaskItemFactory).FullName);

                ITaskItem fromItem = itemFactory.CreateTaskParameterTaskItem();
                fromItem.SetMetadata("a", "a1");
                fromItem.SetMetadata("b", "b1");

                fromItem.CopyMetadataTo(toItem);

                Assert.AreEqual("a1", toItem.GetMetadata("a"));
                Assert.AreEqual("b1", toItem.GetMetadata("b"));
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }
#endif
    }
}
