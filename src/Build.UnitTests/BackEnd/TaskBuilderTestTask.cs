// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A task used for testing the TaskExecutionHost, which reports what the TaskExecutionHost does to it.
    /// </summary>
    public class TaskBuilderTestTask : IGeneratedTask
    {
        /// <summary>
        /// A custom <see cref="IConvertible"/> value type.
        /// </summary>
        /// <remarks>
        /// Types like this one can be used only as Output parameter types because they can be converted to string
        /// but not from string.
        /// </remarks>
        [Serializable]
        public struct CustomStruct : IConvertible
        {
            private readonly object _value;

            /// <summary>
            /// Using <see cref="IConvertible"/> as the type of the <see cref="_value"/> field triggers a BinaryFormatter bug.
            /// </summary>
            private IConvertible Value => (IConvertible)_value;

            public CustomStruct(IConvertible value)
            {
                _value = value;
            }

            public TypeCode GetTypeCode() => Value.GetTypeCode();
            public bool ToBoolean(IFormatProvider provider) => Value.ToBoolean(provider);
            public byte ToByte(IFormatProvider provider) => Value.ToByte(provider);
            public char ToChar(IFormatProvider provider) => Value.ToChar(provider);
            public DateTime ToDateTime(IFormatProvider provider) => Value.ToDateTime(provider);
            public decimal ToDecimal(IFormatProvider provider) => Value.ToDecimal(provider);
            public double ToDouble(IFormatProvider provider) => Value.ToDouble(provider);
            public short ToInt16(IFormatProvider provider) => Value.ToInt16(provider);
            public int ToInt32(IFormatProvider provider) => Value.ToInt32(provider);
            public long ToInt64(IFormatProvider provider) => Value.ToInt64(provider);
            public sbyte ToSByte(IFormatProvider provider) => Value.ToSByte(provider);
            public float ToSingle(IFormatProvider provider) => Value.ToSingle(provider);
            public string ToString(IFormatProvider provider) => Value.ToString(provider);
            public object ToType(Type conversionType, IFormatProvider provider) => Value.ToType(conversionType, provider);
            public ushort ToUInt16(IFormatProvider provider) => Value.ToUInt16(provider);
            public uint ToUInt32(IFormatProvider provider) => Value.ToUInt32(provider);
            public ulong ToUInt64(IFormatProvider provider) => Value.ToUInt64(provider);
        }

        /// <summary>
        /// The <see cref="CustomStruct"/> value returned from <see cref="CustomStructOutput"/>.
        /// </summary>
        internal static readonly CustomStruct s_customStruct = new CustomStruct(42);

        /// <summary>
        /// The <see cref="CustomStruct[]"/> value returned from <see cref="CustomStructArrayOutput"/>.
        /// </summary>
        internal static readonly CustomStruct[] s_customStructArray = new CustomStruct[] { new CustomStruct(43), new CustomStruct(44) };

        /// <summary>
        /// The task host.
        /// </summary>
        private ITestTaskHost _testTaskHost;

        /// <summary>
        /// The value to return from Execute.
        /// </summary>
        private bool _executeReturnValue;

        /// <summary>
        /// The value for the BoolOutput.
        /// </summary>
        private bool _boolOutput;

        /// <summary>
        /// The value for the BoolArrayOutput.
        /// </summary>
        private bool[] _boolArrayOutput;

        /// <summary>
        /// The value for the ByteOutput.
        /// </summary>
        private byte _byteOutput;

        /// <summary>
        /// The value for the ByteArrayOutput.
        /// </summary>
        private byte[] _byteArrayOutput;

        /// <summary>
        /// The value for the SByteOutput.
        /// </summary>
        private sbyte _sbyteOutput;

        /// <summary>
        /// The value for the SByteArrayOutput.
        /// </summary>
        private sbyte[] _sbyteArrayOutput;

        /// <summary>
        /// The value for the DoubleOutput.
        /// </summary>
        private double _doubleOutput;

        /// <summary>
        /// The value for the DoubleArrayOutput.
        /// </summary>
        private double[] _doubleArrayOutput;

        /// <summary>
        /// The value for the FloatOutput.
        /// </summary>
        private float _floatOutput;

        /// <summary>
        /// The value for the FloatArrayOutput.
        /// </summary>
        private float[] _floatArrayOutput;

        /// <summary>
        /// The value for the ShortOutput.
        /// </summary>
        private short _shortOutput;

        /// <summary>
        /// The value for the ShortArrayOutput.
        /// </summary>
        private short[] _shortArrayOutput;

        /// <summary>
        /// The value for the UShortOutput.
        /// </summary>
        private ushort _ushortOutput;

        /// <summary>
        /// The value for the UShortArrayOutput.
        /// </summary>
        private ushort[] _ushortArrayOutput;

        /// <summary>
        /// The value for the IntOutput.
        /// </summary>
        private int _intOutput;

        /// <summary>
        /// The value for the IntArrayOutput.
        /// </summary>
        private int[] _intArrayOutput;

        /// <summary>
        /// The value for the UIntOutput.
        /// </summary>
        private uint _uintOutput;

        /// <summary>
        /// The value for the UIntArrayOutput.
        /// </summary>
        private uint[] _uintArrayOutput;

        /// <summary>
        /// The value for the LongOutput.
        /// </summary>
        private long _longOutput;

        /// <summary>
        /// The value for the LongArrayOutput.
        /// </summary>
        private long[] _longArrayOutput;

        /// <summary>
        /// The value for the ULongOutput.
        /// </summary>
        private ulong _ulongOutput;

        /// <summary>
        /// The value for the ULongArrayOutput.
        /// </summary>
        private ulong[] _ulongArrayOutput;

        /// <summary>
        /// The value for the DecimalOutput.
        /// </summary>
        private decimal _decimalOutput;

        /// <summary>
        /// The value for the DecimalArrayOutput.
        /// </summary>
        private decimal[] _decimalArrayOutput;

        /// <summary>
        /// The value for the CharOutput.
        /// </summary>
        private char _charOutput;

        /// <summary>
        /// The value for the CharArrayOutput.
        /// </summary>
        private char[] _charArrayOutput;

        /// <summary>
        /// The value for the StringOutput.
        /// </summary>
        private string _stringOutput;

        /// <summary>
        /// The value for the StringArrayOutput.
        /// </summary>
        private string[] _stringArrayOutput;

        /// <summary>
        /// The value for the DateTimeOutput.
        /// </summary>
        private DateTime _dateTimeOutput;

        /// <summary>
        /// The value for the DateTimeArrayOutput.
        /// </summary>
        private DateTime[] _dateTimeArrayOutput;

        /// <summary>
        /// The value for the ItemOutput.
        /// </summary>
        private ITaskItem _itemOutput;

        /// <summary>
        /// The value for the ItemArrayOutput.
        /// </summary>
        private ITaskItem[] _itemArrayOutput;

        /// <summary>
        /// Property determining if Execute() should throw or not.
        /// </summary>
        public bool ThrowOnExecute
        {
            internal get;
            set;
        }

        /// <summary>
        /// A boolean parameter.
        /// </summary>
        public bool BoolParam
        {
            set
            {
                _boolOutput = value;
                _testTaskHost?.ParameterSet("BoolParam", value);
            }
        }

        /// <summary>
        /// A boolean array parameter.
        /// </summary>
        public bool[] BoolArrayParam
        {
            set
            {
                _boolArrayOutput = value;
                _testTaskHost?.ParameterSet("BoolArrayParam", value);
            }
        }

        /// <summary>
        /// A byte parameter.
        /// </summary>
        public byte ByteParam
        {
            set
            {
                _byteOutput = value;
                _testTaskHost?.ParameterSet("ByteParam", value);
            }
        }

        /// <summary>
        /// A byte array parameter.
        /// </summary>
        public byte[] ByteArrayParam
        {
            set
            {
                _byteArrayOutput = value;
                _testTaskHost?.ParameterSet("ByteArrayParam", value);
            }
        }

        /// <summary>
        /// An sbyte parameter.
        /// </summary>
        public sbyte SByteParam
        {
            set
            {
                _sbyteOutput = value;
                _testTaskHost?.ParameterSet("SByteParam", value);
            }
        }

        /// <summary>
        /// An sbyte array parameter.
        /// </summary>
        public sbyte[] SByteArrayParam
        {
            set
            {
                _sbyteArrayOutput = value;
                _testTaskHost?.ParameterSet("SByteArrayParam", value);
            }
        }

        /// <summary>
        /// A double parameter.
        /// </summary>
        public double DoubleParam
        {
            set
            {
                _doubleOutput = value;
                _testTaskHost?.ParameterSet("DoubleParam", value);
            }
        }

        /// <summary>
        /// A double array parameter.
        /// </summary>
        public double[] DoubleArrayParam
        {
            set
            {
                _doubleArrayOutput = value;
                _testTaskHost?.ParameterSet("DoubleArrayParam", value);
            }
        }

        /// <summary>
        /// A float parameter.
        /// </summary>
        public float FloatParam
        {
            set
            {
                _floatOutput = value;
                _testTaskHost?.ParameterSet("FloatParam", value);
            }
        }

        /// <summary>
        /// A float array parameter.
        /// </summary>
        public float[] FloatArrayParam
        {
            set
            {
                _floatArrayOutput = value;
                _testTaskHost?.ParameterSet("FloatArrayParam", value);
            }
        }

        /// <summary>
        /// A short parameter.
        /// </summary>
        public short ShortParam
        {
            set
            {
                _shortOutput = value;
                _testTaskHost?.ParameterSet("ShortParam", value);
            }
        }

        /// <summary>
        /// A short array parameter.
        /// </summary>
        public short[] ShortArrayParam
        {
            set
            {
                _shortArrayOutput = value;
                _testTaskHost?.ParameterSet("ShortArrayParam", value);
            }
        }

        /// <summary>
        /// A ushort parameter.
        /// </summary>
        public ushort UShortParam
        {
            set
            {
                _ushortOutput = value;
                _testTaskHost?.ParameterSet("UShortParam", value);
            }
        }

        /// <summary>
        /// A ushort array parameter.
        /// </summary>
        public ushort[] UShortArrayParam
        {
            set
            {
                _ushortArrayOutput = value;
                _testTaskHost?.ParameterSet("UShortArrayParam", value);
            }
        }

        /// <summary>
        /// An integer parameter.
        /// </summary>
        public int IntParam
        {
            set
            {
                _intOutput = value;
                _testTaskHost?.ParameterSet("IntParam", value);
            }
        }

        /// <summary>
        /// An integer array parameter.
        /// </summary>
        public int[] IntArrayParam
        {
            set
            {
                _intArrayOutput = value;
                _testTaskHost?.ParameterSet("IntArrayParam", value);
            }
        }

        /// <summary>
        /// A uint parameter.
        /// </summary>
        public uint UIntParam
        {
            set
            {
                _uintOutput = value;
                _testTaskHost?.ParameterSet("UIntParam", value);
            }
        }

        /// <summary>
        /// A uint array parameter.
        /// </summary>
        public uint[] UIntArrayParam
        {
            set
            {
                _uintArrayOutput = value;
                _testTaskHost?.ParameterSet("UIntArrayParam", value);
            }
        }

        /// <summary>
        /// A long parameter.
        /// </summary>
        public long LongParam
        {
            set
            {
                _longOutput = value;
                _testTaskHost?.ParameterSet("LongParam", value);
            }
        }

        /// <summary>
        /// A long array parameter.
        /// </summary>
        public long[] LongArrayParam
        {
            set
            {
                _longArrayOutput = value;
                _testTaskHost?.ParameterSet("LongArrayParam", value);
            }
        }

        /// <summary>
        /// A ulong parameter.
        /// </summary>
        public ulong ULongParam
        {
            set
            {
                _ulongOutput = value;
                _testTaskHost?.ParameterSet("ULongParam", value);
            }
        }

        /// <summary>
        /// A ulong array parameter.
        /// </summary>
        public ulong[] ULongArrayParam
        {
            set
            {
                _ulongArrayOutput = value;
                _testTaskHost?.ParameterSet("ULongArrayParam", value);
            }
        }

        /// <summary>
        /// A decimal parameter.
        /// </summary>
        public decimal DecimalParam
        {
            set
            {
                _decimalOutput = value;
                _testTaskHost?.ParameterSet("DecimalParam", value);
            }
        }

        /// <summary>
        /// A decimal array parameter.
        /// </summary>
        public decimal[] DecimalArrayParam
        {
            set
            {
                _decimalArrayOutput = value;
                _testTaskHost?.ParameterSet("DecimalArrayParam", value);
            }
        }

        /// <summary>
        /// A char parameter.
        /// </summary>
        public char CharParam
        {
            set
            {
                _charOutput = value;
                _testTaskHost?.ParameterSet("CharParam", value);
            }
        }

        /// <summary>
        /// A char array parameter.
        /// </summary>
        public char[] CharArrayParam
        {
            set
            {
                _charArrayOutput = value;
                _testTaskHost?.ParameterSet("CharArrayParam", value);
            }
        }

        /// <summary>
        /// A string parameter.
        /// </summary>
        public string StringParam
        {
            set
            {
                _stringOutput = value;
                _testTaskHost?.ParameterSet("StringParam", value);
            }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        public string[] StringArrayParam
        {
            set
            {
                _stringArrayOutput = value;
                _testTaskHost?.ParameterSet("StringArrayParam", value);
            }
        }

        /// <summary>
        /// A DateTime parameter.
        /// </summary>
        public DateTime DateTimeParam
        {
            set
            {
                _dateTimeOutput = value;
                _testTaskHost?.ParameterSet("DateTimeParam", value);
            }
        }

        /// <summary>
        /// A DateTime array parameter.
        /// </summary>
        public DateTime[] DateTimeArrayParam
        {
            set
            {
                _dateTimeArrayOutput = value;
                _testTaskHost?.ParameterSet("DateTimeArrayParam", value);
            }
        }

        /// <summary>
        /// An item parameter.
        /// </summary>
        public ITaskItem ItemParam
        {
            set
            {
                _itemOutput = value;
                _testTaskHost?.ParameterSet("ItemParam", value);
            }
        }

        /// <summary>
        /// An item array parameter.
        /// </summary>
        public ITaskItem[] ItemArrayParam
        {
            set
            {
                _itemArrayOutput = value;
                _testTaskHost?.ParameterSet("ItemArrayParam", value);
            }
        }

        /// <summary>
        /// The Execute return value parameter.
        /// </summary>
        [Required]
        public bool ExecuteReturnParam
        {
            set
            {
                _executeReturnValue = value;
                _testTaskHost?.ParameterSet("ExecuteReturnParam", value);
            }
        }

        /// <summary>
        /// A boolean output.
        /// </summary>
        [Output]
        public bool BoolOutput
        {
            get
            {
                _testTaskHost?.OutputRead("BoolOutput", _boolOutput);
                return _boolOutput;
            }
        }

        /// <summary>
        /// A boolean array output.
        /// </summary>
        [Output]
        public bool[] BoolArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("BoolArrayOutput", _boolArrayOutput);
                return _boolArrayOutput;
            }
        }

        /// <summary>
        /// A byte output.
        /// </summary>
        [Output]
        public byte ByteOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ByteOutput", _byteOutput);
                return _byteOutput;
            }
        }

        /// <summary>
        /// A byte array output.
        /// </summary>
        [Output]
        public byte[] ByteArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ByteArrayOutput", _byteArrayOutput);
                return _byteArrayOutput;
            }
        }

        /// <summary>
        /// An sbyte output.
        /// </summary>
        [Output]
        public sbyte SByteOutput
        {
            get
            {
                _testTaskHost?.OutputRead("SByteOutput", _sbyteOutput);
                return _sbyteOutput;
            }
        }

        /// <summary>
        /// An sbyte array output.
        /// </summary>
        [Output]
        public sbyte[] SByteArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("SByteArrayOutput", _sbyteArrayOutput);
                return _sbyteArrayOutput;
            }
        }

        /// <summary>
        /// A double output.
        /// </summary>
        [Output]
        public double DoubleOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DoubleOutput", _doubleOutput);
                return _doubleOutput;
            }
        }

        /// <summary>
        /// A double array output.
        /// </summary>
        [Output]
        public double[] DoubleArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DoubleArrayOutput", _doubleArrayOutput);
                return _doubleArrayOutput;
            }
        }

        /// <summary>
        /// A float output.
        /// </summary>
        [Output]
        public float FloatOutput
        {
            get
            {
                _testTaskHost?.OutputRead("FloatOutput", _floatOutput);
                return _floatOutput;
            }
        }

        /// <summary>
        /// A float array output.
        /// </summary>
        [Output]
        public float[] FloatArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("FloatArrayOutput", _floatArrayOutput);
                return _floatArrayOutput;
            }
        }

        /// <summary>
        /// A short output.
        /// </summary>
        [Output]
        public short ShortOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ShortOutput", _shortOutput);
                return _shortOutput;
            }
        }

        /// <summary>
        /// A short array output.
        /// </summary>
        [Output]
        public short[] ShortArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ShortArrayOutput", _shortArrayOutput);
                return _shortArrayOutput;
            }
        }

        /// <summary>
        /// A ushort output.
        /// </summary>
        [Output]
        public ushort UShortOutput
        {
            get
            {
                _testTaskHost?.OutputRead("UShortOutput", _ushortOutput);
                return _ushortOutput;
            }
        }

        /// <summary>
        /// A ushort array output.
        /// </summary>
        [Output]
        public ushort[] UShortArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("UShortArrayOutput", _ushortArrayOutput);
                return _ushortArrayOutput;
            }
        }

        /// <summary>
        /// An integer output.
        /// </summary>
        [Output]
        public int IntOutput
        {
            get
            {
                _testTaskHost?.OutputRead("IntOutput", _intOutput);
                return _intOutput;
            }
        }

        /// <summary>
        /// An integer array output.
        /// </summary>
        [Output]
        public int[] IntArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("IntArrayOutput", _intArrayOutput);
                return _intArrayOutput;
            }
        }

        /// <summary>
        /// A uint output.
        /// </summary>
        [Output]
        public uint UIntOutput
        {
            get
            {
                _testTaskHost?.OutputRead("UIntOutput", _uintOutput);
                return _uintOutput;
            }
        }

        /// <summary>
        /// A uint array output.
        /// </summary>
        [Output]
        public uint[] UIntArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("UIntArrayOutput", _uintArrayOutput);
                return _uintArrayOutput;
            }
        }

        /// <summary>
        /// A long output.
        /// </summary>
        [Output]
        public long LongOutput
        {
            get
            {
                _testTaskHost?.OutputRead("LongOutput", _longOutput);
                return _longOutput;
            }
        }

        /// <summary>
        /// A long array output.
        /// </summary>
        [Output]
        public long[] LongArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("LongArrayOutput", _longArrayOutput);
                return _longArrayOutput;
            }
        }

        /// <summary>
        /// A ulong output.
        /// </summary>
        [Output]
        public ulong ULongOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ULongOutput", _ulongOutput);
                return _ulongOutput;
            }
        }

        /// <summary>
        /// A ulong array output.
        /// </summary>
        [Output]
        public ulong[] ULongArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ULongArrayOutput", _ulongArrayOutput);
                return _ulongArrayOutput;
            }
        }

        /// <summary>
        /// A decimal output.
        /// </summary>
        [Output]
        public decimal DecimalOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DecimalOutput", _decimalOutput);
                return _decimalOutput;
            }
        }

        /// <summary>
        /// A decimal array output.
        /// </summary>
        [Output]
        public decimal[] DecimalArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DecimalArrayOutput", _decimalArrayOutput);
                return _decimalArrayOutput;
            }
        }

        /// <summary>
        /// A char output.
        /// </summary>
        [Output]
        public char CharOutput
        {
            get
            {
                _testTaskHost?.OutputRead("CharOutput", _charOutput);
                return _charOutput;
            }
        }

        /// <summary>
        /// A char array output.
        /// </summary>
        [Output]
        public char[] CharArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("CharArrayOutput", _charArrayOutput);
                return _charArrayOutput;
            }
        }

        /// <summary>
        /// A string output.
        /// </summary>
        [Output]
        public string StringOutput
        {
            get
            {
                _testTaskHost?.OutputRead("StringOutput", _stringOutput);
                return _stringOutput;
            }
        }

        /// <summary>
        /// An empty string output
        /// </summary>
        [Output]
        public string EmptyStringOutput
        {
            get
            {
                _testTaskHost?.OutputRead("EmptyStringOutput", null);
                return String.Empty;
            }
        }

        /// <summary>
        /// An empty string array output
        /// </summary>
        [Output]
        public string[] EmptyStringArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("EmptyStringArrayOutput", null);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// A null string output
        /// </summary>
        [Output]
        public string NullStringOutput
        {
            get
            {
                _testTaskHost?.OutputRead("NullStringOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A DateTime output
        /// </summary>
        [Output]
        public DateTime DateTimeOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DateTimeOutput", _dateTimeOutput);
                return _dateTimeOutput;
            }
        }

        /// <summary>
        /// A DateTime array output.
        /// </summary>
        [Output]
        public DateTime[] DateTimeArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("DateTimeArrayOutput", _dateTimeArrayOutput);
                return _dateTimeArrayOutput;
            }
        }

        /// <summary>
        /// A CustomStruct output.
        /// </summary>
        [Output]
        public CustomStruct CustomStructOutput
        {
            get
            {
                _testTaskHost?.OutputRead("CustomStructOutput", s_customStruct);
                return s_customStruct;
            }
        }

        /// <summary>
        /// A CustomStruct array output.
        /// </summary>
        [Output]
        public CustomStruct[] CustomStructArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("CustomStructArrayOutput", s_customStructArray);
                return s_customStructArray;
            }
        }

        /// <summary>
        /// A null ITaskItem output.
        /// </summary>
        [Output]
        public ITaskItem NullITaskItemOutput
        {
            get
            {
                _testTaskHost?.OutputRead("NullITaskItemOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A null string array output.
        /// </summary>
        [Output]
        public string[] NullStringArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("NullStringArrayOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A null ITaskItem array output.
        /// </summary>
        [Output]
        public ITaskItem[] NullITaskItemArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("NullITaskItemArrayOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A string array output.
        /// </summary>
        [Output]
        public string[] StringArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("StringArrayOutput", _stringArrayOutput);
                return _stringArrayOutput;
            }
        }

        /// <summary>
        /// A task item output.
        /// </summary>
        [Output]
        public ITaskItem ItemOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ItemOutput", _itemOutput);
                return _itemOutput;
            }
        }

        /// <summary>
        /// A task item array output.
        /// </summary>
        [Output]
        public ITaskItem[] ItemArrayOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ItemArrayOutput", _itemArrayOutput);
                return _itemArrayOutput;
            }
        }

        /// <summary>
        /// A task item array output that is null.
        /// </summary>
        [Output]
        public ITaskItem[] ItemArrayNullOutput
        {
            get
            {
                _testTaskHost?.OutputRead("ItemArrayNullOutput", _itemArrayOutput);
                return null;
            }
        }

        [Output]
        public TargetBuiltReason EnumOutput => TargetBuiltReason.BeforeTargets;

        #region ITask Members

        /// <summary>
        /// The build engine property
        /// </summary>
        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        /// <summary>
        /// The host object property
        /// </summary>
        public ITaskHost HostObject
        {
            get
            {
                return _testTaskHost;
            }

            set
            {
                _testTaskHost = value as ITestTaskHost;
            }
        }

        /// <summary>
        /// The Execute() method for ITask.
        /// </summary>
        public bool Execute()
        {
            if (ThrowOnExecute)
            {
                throw new IndexOutOfRangeException();
            }

            return _executeReturnValue;
        }

        #endregion

        #region IGeneratedTask Members

        /// <summary>
        /// Gets the property value.
        /// </summary>
        /// <param name="property">The property to get.</param>
        /// <returns>
        /// The value of the property, the value's type will match the type given by <see cref="TaskPropertyInfo.PropertyType"/>.
        /// </returns>
        /// <remarks>
        /// MSBuild calls this method after executing the task to get output parameters.
        /// All exceptions from this method will be caught in the taskExecution host and logged as a fatal task error
        /// </remarks>
        public object GetPropertyValue(TaskPropertyInfo property)
        {
            return GetType().GetProperty(property.Name).GetValue(this, null);
        }

        /// <summary>
        /// Sets a value on a property of this task instance.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="value">The value to set. The caller is responsible to type-coerce this value to match the property's <see cref="TaskPropertyInfo.PropertyType"/>.</param>
        /// <remarks>
        /// All exceptions from this method will be caught in the taskExecution host and logged as a fatal task error
        /// </remarks>
        public void SetPropertyValue(TaskPropertyInfo property, object value)
        {
            GetType().GetProperty(property.Name).SetValue(this, value, null);
        }

        #endregion

        /// <summary>
        /// Task factory which wraps a test task, this is used for unit testing
        /// </summary>
        internal sealed class TaskBuilderTestTaskFactory : ITaskFactory
        {
            /// <summary>
            /// Type of the task wrapped by the task factory
            /// </summary>
            private Type _type = typeof(TaskBuilderTestTask);

            /// <summary>
            /// Should the task throw on execution
            /// </summary>
            public bool ThrowOnExecute
            {
                get;
                set;
            }

            /// <summary>
            /// Name of the factory
            /// </summary>
            public string FactoryName
            {
                get { return typeof(TaskBuilderTestTask).ToString(); }
            }

            /// <summary>
            /// Gets the type of task generated.
            /// </summary>
            public Type TaskType
            {
                get { return _type; }
            }

            /// <summary>
            /// There is nothing to initialize
            /// </summary>
            public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> taskParameters, string taskElementContents, IBuildEngine taskLoggingHost)
            {
                return true;
            }

            /// <summary>
            /// Get a list of parameters for the task.
            /// </summary>
            public TaskPropertyInfo[] GetTaskParameters()
            {
                PropertyInfo[] infos = _type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var propertyInfos = new TaskPropertyInfo[infos.Length];
                for (int i = 0; i < infos.Length; i++)
                {
                    propertyInfos[i] = new TaskPropertyInfo(
                        infos[i].Name,
                        infos[i].PropertyType,
                        infos[i].GetCustomAttributes(typeof(OutputAttribute), false).Length > 0,
                        infos[i].GetCustomAttributes(typeof(RequiredAttribute), false).Length > 0);
                }

                return propertyInfos;
            }

            /// <summary>
            /// Create a new instance
            /// </summary>
            public ITask CreateTask(IBuildEngine loggingHost)
            {
                var task = new TaskBuilderTestTask();
                task.ThrowOnExecute = ThrowOnExecute;
                return task;
            }

            /// <summary>
            ///  Cleans up a task that is finished.
            /// </summary>
            public void CleanupTask(ITask task)
            {
            }
        }
    }
}
