// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
#if FEATURE_APPDOMAIN
using System.Security;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Type of parameter, used to figure out how to serialize it.
    /// </summary>
    internal enum TaskParameterType
    {
        /// <summary>
        /// Parameter is null.
        /// </summary>
        Null,

        /// <summary>
        /// Parameter is of a type described by a <see cref="TypeCode"/>.
        /// </summary>
        PrimitiveType,

        /// <summary>
        /// Parameter is an array of a type described by a <see cref="TypeCode"/>.
        /// </summary>
        PrimitiveTypeArray,

        /// <summary>
        /// Parameter is a value type.  Note:  Must be <see cref="IConvertible"/>.
        /// </summary>
        ValueType,

        /// <summary>
        /// Parameter is an array of value types.  Note:  Must be <see cref="IConvertible"/>.
        /// </summary>
        ValueTypeArray,

        /// <summary>
        /// Parameter is an ITaskItem.
        /// </summary>
        ITaskItem,

        /// <summary>
        /// Parameter is an array of ITaskItems.
        /// </summary>
        ITaskItemArray,

        /// <summary>
        /// An invalid parameter -- the value of this parameter contains the exception
        /// that is thrown when trying to access it.
        /// </summary>
        Invalid,
    }

    /// <summary>
    /// Wrapper for task parameters, to allow proper serialization even
    /// in cases where the parameter is not .NET serializable.
    /// </summary>
    internal class TaskParameter :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        ITranslatable
    {
        /// <summary>
        /// The TaskParameterType of the wrapped parameter.
        /// </summary>
        private TaskParameterType _parameterType;

        /// <summary>
        /// The <see cref="TypeCode"/> of the wrapped parameter if it's a primitive type.
        /// </summary>
        private TypeCode _parameterTypeCode;

        /// <summary>
        /// The actual task parameter that we're wrapping
        /// </summary>
        private object _wrappedParameter;

        /// <summary>
        /// Create a new TaskParameter
        /// </summary>
        public TaskParameter(object wrappedParameter)
        {
            if (wrappedParameter == null)
            {
                _parameterType = TaskParameterType.Null;
                _wrappedParameter = null;
                return;
            }

            Type wrappedParameterType = wrappedParameter.GetType();

            if (wrappedParameter is Exception)
            {
                _parameterType = TaskParameterType.Invalid;
                _wrappedParameter = wrappedParameter;
                return;
            }

            // It's not null or invalid, so it should be a valid parameter type.
            ErrorUtilities.VerifyThrow(
                    TaskParameterTypeVerifier.IsValidInputParameter(wrappedParameterType) || TaskParameterTypeVerifier.IsValidOutputParameter(wrappedParameterType),
                    "How did we manage to get a task parameter of type {0} that isn't a valid parameter type?",
                    wrappedParameterType);

            if (wrappedParameterType.IsArray)
            {
                TypeCode typeCode = Type.GetTypeCode(wrappedParameterType.GetElementType());
                if (typeCode != TypeCode.Object && typeCode != TypeCode.DBNull)
                {
                    _parameterType = TaskParameterType.PrimitiveTypeArray;
                    _parameterTypeCode = typeCode;
                    _wrappedParameter = wrappedParameter;
                }
                else if (typeof(ITaskItem[]).GetTypeInfo().IsAssignableFrom(wrappedParameterType.GetTypeInfo()))
                {
                    _parameterType = TaskParameterType.ITaskItemArray;
                    ITaskItem[] inputAsITaskItemArray = (ITaskItem[])wrappedParameter;
                    ITaskItem[] taskItemArrayParameter = new ITaskItem[inputAsITaskItemArray.Length];

                    for (int i = 0; i < inputAsITaskItemArray.Length; i++)
                    {
                        if (inputAsITaskItemArray[i] != null)
                        {
                            taskItemArrayParameter[i] = CreateNewTaskItemFrom(inputAsITaskItemArray[i]);
                        }
                    }

                    _wrappedParameter = taskItemArrayParameter;
                }
                else if (wrappedParameterType.GetElementType().GetTypeInfo().IsValueType)
                {
                    _parameterType = TaskParameterType.ValueTypeArray;
                    _wrappedParameter = wrappedParameter;
                }
                else
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                }
            }
            else
            {
                // scalar parameter
                // Preserve enums as strings: the enum type itself may not
                // be loaded on the other side of the serialization, but
                // we would convert to string anyway after pulling the
                // task output into a property or item.
                if (wrappedParameterType.IsEnum)
                {
                    wrappedParameter = (string)Convert.ChangeType(wrappedParameter, typeof(string), CultureInfo.InvariantCulture);
                    wrappedParameterType = typeof(string);
                }

                TypeCode typeCode = Type.GetTypeCode(wrappedParameterType);
                if (typeCode != TypeCode.Object && typeCode != TypeCode.DBNull)
                {
                    _parameterType = TaskParameterType.PrimitiveType;
                    _parameterTypeCode = typeCode;
                    _wrappedParameter = wrappedParameter;
                }
                else if (typeof(ITaskItem).IsAssignableFrom(wrappedParameterType))
                {
                    _parameterType = TaskParameterType.ITaskItem;
                    _wrappedParameter = CreateNewTaskItemFrom((ITaskItem)wrappedParameter);
                }
                else if (wrappedParameterType.GetTypeInfo().IsValueType)
                {
                    _parameterType = TaskParameterType.ValueType;
                    _wrappedParameter = wrappedParameter;
                }
                else
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                }
            }
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        private TaskParameter()
        {
        }

        /// <summary>
        /// The TaskParameterType of the wrapped parameter.
        /// </summary>
        public TaskParameterType ParameterType => _parameterType;

        /// <summary>
        /// The <see cref="TypeCode"/> of the wrapper parameter if it's a primitive or array of primitives.
        /// </summary>
        public TypeCode ParameterTypeCode => _parameterTypeCode;

        /// <summary>
        /// The actual task parameter that we're wrapping.
        /// </summary>
        public object WrappedParameter => _wrappedParameter;

        /// <summary>
        /// TaskParameter's ToString should just pass through to whatever it's wrapping.
        /// </summary>
        public override string ToString()
        {
            return (WrappedParameter == null) ? String.Empty : WrappedParameter.ToString();
        }

        /// <summary>
        /// Serialize / deserialize this item.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _parameterType, (int)_parameterType);

            switch (_parameterType)
            {
                case TaskParameterType.Null:
                    _wrappedParameter = null;
                    break;
                case TaskParameterType.PrimitiveType:
                    TranslatePrimitiveType(translator);
                    break;
                case TaskParameterType.PrimitiveTypeArray:
                    TranslatePrimitiveTypeArray(translator);
                    break;
                case TaskParameterType.ValueType:
                    if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_8))
                    {
                        TranslateValueType(translator);
                    }
                    else
                    {
                        translator.TranslateDotNet(ref _wrappedParameter);
                    }
                    break;
                case TaskParameterType.ValueTypeArray:
                    if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_8))
                    {
                        TranslateValueTypeArray(translator);
                    }
                    else
                    {
                        translator.TranslateDotNet(ref _wrappedParameter);
                    }
                    break;
                case TaskParameterType.ITaskItem:
                    TranslateITaskItem(translator);
                    break;
                case TaskParameterType.ITaskItemArray:
                    TranslateITaskItemArray(translator);
                    break;
                case TaskParameterType.Invalid:
                    Exception exceptionParam = (Exception)_wrappedParameter;
                    translator.TranslateException(ref exceptionParam);
                    _wrappedParameter = exceptionParam;
                    break;
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    break;
            }
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Overridden to give this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and instances can expire if they take long time processing.
        /// </summary>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            // null means infinite lease time
            return null;
        }
#endif

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static TaskParameter FactoryForDeserialization(ITranslator translator)
        {
            TaskParameter taskParameter = new TaskParameter();
            taskParameter.Translate(translator);
            return taskParameter;
        }

        /// <summary>
        /// Creates a new ITaskItem with the contents of the old one.
        /// </summary>
        private ITaskItem CreateNewTaskItemFrom(ITaskItem copyFrom)
        {
            ITaskItem2 copyFromAsITaskItem2 = copyFrom as ITaskItem2;
            string escapedItemSpec;
            string escapedDefiningProject;
            Dictionary<string, string> escapedMetadata;
            if (copyFromAsITaskItem2 != null)
            {
                escapedItemSpec = copyFromAsITaskItem2.EvaluatedIncludeEscaped;
                escapedDefiningProject = copyFromAsITaskItem2.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);
                IDictionary nonGenericEscapedMetadata = copyFromAsITaskItem2.CloneCustomMetadataEscaped();

                escapedMetadata = nonGenericEscapedMetadata as Dictionary<string, string>;
                if (escapedMetadata is null)
                {
                    escapedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (object key in nonGenericEscapedMetadata.Keys)
                    {
                        escapedMetadata[(string)key] = (string)nonGenericEscapedMetadata[key] ?? String.Empty;
                    }
                }
            }
            else
            {
                // If we don't have ITaskItem2 to fall back on, we have to make do with the fact that
                // CloneCustomMetadata, GetMetadata, & ItemSpec returns unescaped values, and
                // TaskParameterTaskItem's constructor expects escaped values, so escaping them all
                // is the closest approximation to correct we can get.
                escapedItemSpec = EscapingUtilities.Escape(copyFrom.ItemSpec);

                escapedDefiningProject = EscapingUtilities.EscapeWithCaching(copyFrom.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath));

                IDictionary customMetadata = copyFrom.CloneCustomMetadata();
                escapedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (customMetadata?.Count > 0)
                {
                    foreach (string key in customMetadata.Keys)
                    {
                        escapedMetadata.Add(key, EscapingUtilities.Escape((string)customMetadata[key] ?? String.Empty));
                    }
                }
            }

            TaskParameterTaskItem taskItem = new TaskParameterTaskItem(escapedItemSpec, escapedDefiningProject, escapedMetadata);
            return taskItem;
        }

        /// <summary>
        /// Serialize / deserialize this item.
        /// </summary>
        private void TranslateITaskItemArray(ITranslator translator)
        {
            if (!TranslateNullable(translator, _wrappedParameter))
            {
                return;
            }

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                ITaskItem[] wrappedItems = (ITaskItem[])_wrappedParameter;

                int length = wrappedItems.Length;
                translator.Translate(ref length);

                foreach (ITaskItem wrappedItem in wrappedItems)
                {
                    WriteITaskItem(translator, wrappedItem);
                }
            }
            else
            {
                int length = 0;
                translator.Translate(ref length);
                ITaskItem[] wrappedItems = new ITaskItem[length];

                for (int i = 0; i < length; i++)
                {
                    ReadITaskItem(translator, ref wrappedItems[i]);
                }

                _wrappedParameter = wrappedItems;
            }
        }

        /// <summary>
        /// Serialize / deserialize this item.
        /// </summary>
        private void TranslateITaskItem(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                WriteITaskItem(translator, (ITaskItem)_wrappedParameter);
            }
            else // TranslationDirection.ReadFromStream
            {
                ITaskItem wrappedItem = null;
                ReadITaskItem(translator, ref wrappedItem);
                _wrappedParameter = wrappedItem;
            }
        }

        /// <summary>
        /// Write the given ITaskItem, using the given write translator
        /// </summary>
        private void WriteITaskItem(ITranslator translator, ITaskItem wrappedItem)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "Cannot call this method when reading!");

            if (!TranslateNullable(translator, wrappedItem))
            {
                return;
            }

            string escapedItemSpec;
            string escapedDefiningProject;
            IDictionary wrappedMetadata;
            bool wrappedMetadataIsEscaped;

            ITaskItem2 wrappedItemAsITaskItem2 = wrappedItem as ITaskItem2;

            if (wrappedItemAsITaskItem2 != null)
            {
                escapedItemSpec = wrappedItemAsITaskItem2.EvaluatedIncludeEscaped;
                escapedDefiningProject = wrappedItemAsITaskItem2.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);
                wrappedMetadata = wrappedItemAsITaskItem2.CloneCustomMetadataEscaped();
                wrappedMetadataIsEscaped = true;
            }
            else
            {
                // We know that the ITaskItem constructor expects an escaped string, and that ITaskItem.ItemSpec
                // is expected to be unescaped, so make sure we give the constructor what it wants.
                escapedItemSpec = EscapingUtilities.Escape(wrappedItem.ItemSpec);
                escapedDefiningProject = EscapingUtilities.EscapeWithCaching(wrappedItem.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath));
                wrappedMetadata = wrappedItem.CloneCustomMetadata();
                wrappedMetadataIsEscaped = false;
            }

            Dictionary<string, string> escapedGenericWrappedMetadata = wrappedMetadata as Dictionary<string, string>;

            if (escapedGenericWrappedMetadata == null)
            {
                escapedGenericWrappedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (object key in wrappedMetadata.Keys)
                {
                    string value = (string)wrappedMetadata[key];

                    if (!wrappedMetadataIsEscaped)
                    {
                        value = (value == null) ? value : EscapingUtilities.Escape(value);
                    }

                    escapedGenericWrappedMetadata.Add((string)key, value);
                }
            }
            else if (!wrappedMetadataIsEscaped)
            {
                foreach (KeyValuePair<string, string> entry in escapedGenericWrappedMetadata)
                {
                    escapedGenericWrappedMetadata[entry.Key] = entry.Value == null ? entry.Value : EscapingUtilities.Escape(entry.Value);
                }
            }

            translator.Translate(ref escapedItemSpec);
            translator.Translate(ref escapedDefiningProject);
            translator.TranslateDictionary(ref escapedGenericWrappedMetadata, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Read an ITaskItem into the given parameter, using the given read translator
        /// </summary>
        private void ReadITaskItem(ITranslator translator, ref ITaskItem wrappedItem)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.ReadFromStream, "Cannot call this method when writing!");

            if (!TranslateNullable(translator, wrappedItem))
            {
                return;
            }

            string escapedItemSpec = null;
            string escapedDefiningProject = null;
            Dictionary<string, string> escapedMetadata = null;

            translator.Translate(ref escapedItemSpec);
            translator.Translate(ref escapedDefiningProject);
            translator.TranslateDictionary(ref escapedMetadata, StringComparer.OrdinalIgnoreCase);

            wrappedItem = new TaskParameterTaskItem(escapedItemSpec, escapedDefiningProject, escapedMetadata);
        }

        /// <summary>
        /// Writes out the boolean which says if this object is null or not.
        /// </summary>
        /// <typeparam name="T">The nullable type to translate.</typeparam>
        private bool TranslateNullable<T>(ITranslator translator, T value)
        {
            bool haveRef = false;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                haveRef = (value != null);
                translator.Translate(ref haveRef);
            }
            else // TranslationDirection.ReadFromStream
            {
                translator.Translate(ref haveRef);
            }

            return haveRef;
        }

        /// <summary>
        /// Serializes or deserializes a primitive type value wrapped by this <see cref="TaskParameter"/>.
        /// </summary>
        private void TranslatePrimitiveType(ITranslator translator)
        {
            translator.TranslateEnum(ref _parameterTypeCode, (int)_parameterTypeCode);

            switch (_parameterTypeCode)
            {
                case TypeCode.Boolean:
                    bool boolParam = _wrappedParameter is bool wrappedBool ? wrappedBool : default;
                    translator.Translate(ref boolParam);
                    _wrappedParameter = boolParam;
                    break;

                case TypeCode.Byte:
                    byte byteParam = _wrappedParameter is byte wrappedByte ? wrappedByte : default;
                    translator.Translate(ref byteParam);
                    _wrappedParameter = byteParam;
                    break;

                case TypeCode.Int16:
                    short shortParam = _wrappedParameter is short wrappedShort ? wrappedShort : default;
                    translator.Translate(ref shortParam);
                    _wrappedParameter = shortParam;
                    break;

                case TypeCode.UInt16:
                    ushort ushortParam = _wrappedParameter is ushort wrappedUShort ? wrappedUShort : default;
                    translator.Translate(ref ushortParam);
                    _wrappedParameter = ushortParam;
                    break;

                case TypeCode.Int64:
                    long longParam = _wrappedParameter is long wrappedLong ? wrappedLong : default;
                    translator.Translate(ref longParam);
                    _wrappedParameter = longParam;
                    break;

                case TypeCode.Double:
                    double doubleParam = _wrappedParameter is double wrappedDouble ? wrappedDouble : default;
                    translator.Translate(ref doubleParam);
                    _wrappedParameter = doubleParam;
                    break;

                case TypeCode.String:
                    string stringParam = (string)_wrappedParameter;
                    translator.Translate(ref stringParam);
                    _wrappedParameter = stringParam;
                    break;

                case TypeCode.DateTime:
                    DateTime dateTimeParam = _wrappedParameter is DateTime wrappedDateTime ? wrappedDateTime : default;
                    translator.Translate(ref dateTimeParam);
                    _wrappedParameter = dateTimeParam;
                    break;

                default:
                    // Fall back to converting to/from string for types that don't have ITranslator support.
                    string stringValue = null;
                    if (translator.Mode == TranslationDirection.WriteToStream)
                    {
                        stringValue = (string)Convert.ChangeType(_wrappedParameter, typeof(string), CultureInfo.InvariantCulture);
                    }

                    translator.Translate(ref stringValue);

                    if (translator.Mode == TranslationDirection.ReadFromStream)
                    {
                        _wrappedParameter = Convert.ChangeType(stringValue, _parameterTypeCode, CultureInfo.InvariantCulture);
                    }
                    break;
            }
        }

        /// <summary>
        /// Serializes or deserializes an array of primitive type values wrapped by this <see cref="TaskParameter"/>.
        /// </summary>
        private void TranslatePrimitiveTypeArray(ITranslator translator)
        {
            translator.TranslateEnum(ref _parameterTypeCode, (int)_parameterTypeCode);

            switch (_parameterTypeCode)
            {
                case TypeCode.Boolean:
                    bool[] boolArrayParam = (bool[])_wrappedParameter;
                    translator.Translate(ref boolArrayParam);
                    _wrappedParameter = boolArrayParam;
                    break;

                case TypeCode.Int32:
                    int[] intArrayParam = (int[])_wrappedParameter;
                    translator.Translate(ref intArrayParam);
                    _wrappedParameter = intArrayParam;
                    break;

                case TypeCode.String:
                    string[] stringArrayParam = (string[])_wrappedParameter;
                    translator.Translate(ref stringArrayParam);
                    _wrappedParameter = stringArrayParam;
                    break;

                default:
                    // Fall back to converting to/from string for types that don't have ITranslator support.
                    if (translator.Mode == TranslationDirection.WriteToStream)
                    {
                        Array array = (Array)_wrappedParameter;
                        int length = array.Length;

                        translator.Translate(ref length);

                        for (int i = 0; i < length; i++)
                        {
                            string valueString = Convert.ToString(array.GetValue(i), CultureInfo.InvariantCulture);
                            translator.Translate(ref valueString);
                        }
                    }
                    else
                    {
                        Type elementType = _parameterTypeCode switch
                        {
                            TypeCode.Char => typeof(char),
                            TypeCode.SByte => typeof(sbyte),
                            TypeCode.Byte => typeof(byte),
                            TypeCode.Int16 => typeof(short),
                            TypeCode.UInt16 => typeof(ushort),
                            TypeCode.UInt32 => typeof(uint),
                            TypeCode.Int64 => typeof(long),
                            TypeCode.UInt64 => typeof(ulong),
                            TypeCode.Single => typeof(float),
                            TypeCode.Double => typeof(double),
                            TypeCode.Decimal => typeof(decimal),
                            TypeCode.DateTime => typeof(DateTime),
                            _ => throw new NotImplementedException(),
                        };

                        int length = 0;
                        translator.Translate(ref length);

                        Array array = Array.CreateInstance(elementType, length);
                        for (int i = 0; i < length; i++)
                        {
                            string valueString = null;
                            translator.Translate(ref valueString);
                            array.SetValue(Convert.ChangeType(valueString, _parameterTypeCode, CultureInfo.InvariantCulture), i);
                        }
                        _wrappedParameter = array;
                    }
                    break;
            }
        }

        /// <summary>
        /// Serializes or deserializes the value type instance wrapped by this <see cref="TaskParameter"/>.
        /// </summary>
        /// <remarks>
        /// The value type is converted to/from string using the <see cref="Convert"/> class. Note that we require
        /// task parameter types to be <see cref="IConvertible"/> so this conversion is guaranteed to work for parameters
        /// that have made it this far.
        /// </remarks>
        private void TranslateValueType(ITranslator translator)
        {
            string valueString = null;
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                valueString = (string)Convert.ChangeType(_wrappedParameter, typeof(string), CultureInfo.InvariantCulture);
            }

            translator.Translate(ref valueString);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                // We don't know how to convert the string back to the original value type. This is fine because output
                // task parameters are anyway converted to strings by the engine (see TaskExecutionHost.GetValueOutputs)
                // and input task parameters of custom value types are not supported.
                _wrappedParameter = valueString;
            }
        }

        /// <summary>
        /// Serializes or deserializes the value type array instance wrapped by this <see cref="TaskParameter"/>.
        /// </summary>
        /// <remarks>
        /// The array is assumed to be non-null.
        /// </remarks>
        private void TranslateValueTypeArray(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                Array array = (Array)_wrappedParameter;
                int length = array.Length;

                translator.Translate(ref length);

                for (int i = 0; i < length; i++)
                {
                    string valueString = Convert.ToString(array.GetValue(i), CultureInfo.InvariantCulture);
                    translator.Translate(ref valueString);
                }
            }
            else
            {
                int length = 0;
                translator.Translate(ref length);

                string[] stringArray = new string[length];
                for (int i = 0; i < length; i++)
                {
                    translator.Translate(ref stringArray[i]);
                }

                // We don't know how to convert the string array back to the original value type array.
                // This is fine because the engine would eventually convert it to strings anyway.
                _wrappedParameter = stringArray;
            }
        }

        /// <summary>
        /// Super simple ITaskItem derivative that we can use as a container for read items.
        /// </summary>
        private class TaskParameterTaskItem :
#if FEATURE_APPDOMAIN
            MarshalByRefObject,
#endif
            ITaskItem,
            ITaskItem2
#if !TASKHOST
            , IMetadataContainer
#endif
        {
            /// <summary>
            /// The item spec
            /// </summary>
            private string _escapedItemSpec = null;

            /// <summary>
            /// The full path to the project that originally defined this item.
            /// </summary>
            private string _escapedDefiningProject = null;

            /// <summary>
            /// The custom metadata
            /// </summary>
            private Dictionary<string, string> _customEscapedMetadata = null;

            /// <summary>
            /// Cache for fullpath metadata
            /// </summary>
            private string _fullPath;

            /// <summary>
            /// Constructor for serialization
            /// </summary>
            public TaskParameterTaskItem(string escapedItemSpec, string escapedDefiningProject, Dictionary<string, string> escapedMetadata)
            {
                ErrorUtilities.VerifyThrowInternalNull(escapedItemSpec, nameof(escapedItemSpec));

                _escapedItemSpec = escapedItemSpec;
                _escapedDefiningProject = escapedDefiningProject;
                _customEscapedMetadata = escapedMetadata;
            }

            /// <summary>
            /// Gets or sets the item "specification" e.g. for disk-based items this would be the file path.
            /// </summary>
            /// <remarks>
            /// This should be named "EvaluatedInclude" but that would be a breaking change to this interface.
            /// </remarks>
            /// <value>The item-spec string.</value>
            public string ItemSpec
            {
                get
                {
                    return (_escapedItemSpec == null) ? String.Empty : EscapingUtilities.UnescapeAll(_escapedItemSpec);
                }

                set
                {
                    _escapedItemSpec = value;
                }
            }

            /// <summary>
            /// Gets the names of all the metadata on the item.
            /// Includes the built-in metadata like "FullPath".
            /// </summary>
            /// <value>The list of metadata names.</value>
            public ICollection MetadataNames
            {
                get
                {
                    List<string> metadataNames = (_customEscapedMetadata == null) ? new List<string>() : new List<string>(_customEscapedMetadata.Keys);
                    metadataNames.AddRange(FileUtilities.ItemSpecModifiers.All);

                    return metadataNames;
                }
            }

            /// <summary>
            /// Gets the number of pieces of metadata on the item. Includes
            /// both custom and built-in metadata.  Used only for unit testing.
            /// </summary>
            /// <value>Count of pieces of metadata.</value>
            public int MetadataCount
            {
                get
                {
                    int count = (_customEscapedMetadata == null) ? 0 : _customEscapedMetadata.Count;
                    return count + FileUtilities.ItemSpecModifiers.All.Length;
                }
            }

            /// <summary>
            /// Returns the escaped version of this item's ItemSpec
            /// </summary>
            string ITaskItem2.EvaluatedIncludeEscaped
            {
                get
                {
                    return _escapedItemSpec;
                }

                set
                {
                    _escapedItemSpec = value;
                }
            }

            /// <summary>
            /// Allows the values of metadata on the item to be queried.
            /// </summary>
            /// <param name="metadataName">The name of the metadata to retrieve.</param>
            /// <returns>The value of the specified metadata.</returns>
            public string GetMetadata(string metadataName)
            {
                string metadataValue = (this as ITaskItem2).GetMetadataValueEscaped(metadataName);
                return EscapingUtilities.UnescapeAll(metadataValue);
            }

            /// <summary>
            /// Allows a piece of custom metadata to be set on the item.
            /// </summary>
            /// <param name="metadataName">The name of the metadata to set.</param>
            /// <param name="metadataValue">The metadata value.</param>
            public void SetMetadata(string metadataName, string metadataValue)
            {
                ErrorUtilities.VerifyThrowArgumentLength(metadataName, nameof(metadataName));

                // Non-derivable metadata can only be set at construction time.
                // That's why this is IsItemSpecModifier and not IsDerivableItemSpecModifier.
                ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName), "Shared.CannotChangeItemSpecModifiers", metadataName);

                _customEscapedMetadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                _customEscapedMetadata[metadataName] = metadataValue ?? String.Empty;
            }

            /// <summary>
            /// Allows the removal of custom metadata set on the item.
            /// </summary>
            /// <param name="metadataName">The name of the metadata to remove.</param>
            public void RemoveMetadata(string metadataName)
            {
                ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));
                ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName), "Shared.CannotChangeItemSpecModifiers", metadataName);

                if (_customEscapedMetadata == null)
                {
                    return;
                }

                _customEscapedMetadata.Remove(metadataName);
            }

            /// <summary>
            /// Allows custom metadata on the item to be copied to another item.
            /// </summary>
            /// <remarks>
            /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
            /// 1) this method should NOT copy over the item-spec
            /// 2) if a particular piece of metadata already exists on the destination item, it should NOT be overwritten
            /// 3) if there are pieces of metadata on the item that make no semantic sense on the destination item, they should NOT be copied
            /// </remarks>
            /// <param name="destinationItem">The item to copy metadata to.</param>
            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                ErrorUtilities.VerifyThrowArgumentNull(destinationItem, nameof(destinationItem));

                // also copy the original item-spec under a "magic" metadata -- this is useful for tasks that forward metadata
                // between items, and need to know the source item where the metadata came from
                string originalItemSpec = destinationItem.GetMetadata("OriginalItemSpec");

                if (_customEscapedMetadata != null)
                {
                    foreach (KeyValuePair<string, string> entry in _customEscapedMetadata)
                    {
                        string value = destinationItem.GetMetadata(entry.Key);

                        if (String.IsNullOrEmpty(value))
                        {
                            destinationItem.SetMetadata(entry.Key, entry.Value);
                        }
                    }
                }

                if (String.IsNullOrEmpty(originalItemSpec))
                {
                    destinationItem.SetMetadata("OriginalItemSpec", EscapingUtilities.Escape(ItemSpec));
                }
            }

            /// <summary>
            /// Get the collection of custom metadata. This does not include built-in metadata.
            /// </summary>
            /// <remarks>
            /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
            /// 1) this method should return a clone of the metadata
            /// 2) writing to this dictionary should not be reflected in the underlying item.
            /// </remarks>
            /// <returns>Dictionary of cloned metadata</returns>
            public IDictionary CloneCustomMetadata()
            {
                IDictionary<string, string> clonedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (_customEscapedMetadata != null)
                {
                    foreach (KeyValuePair<string, string> metadatum in _customEscapedMetadata)
                    {
                        clonedMetadata.Add(metadatum.Key, EscapingUtilities.UnescapeAll(metadatum.Value));
                    }
                }

                return (IDictionary)clonedMetadata;
            }

#if FEATURE_APPDOMAIN
            /// <summary>
            /// Overridden to give this class infinite lease time. Otherwise we end up with a limited
            /// lease (5 minutes I think) and instances can expire if they take long time processing.
            /// </summary>
            [SecurityCritical]
            public override object InitializeLifetimeService()
            {
                // null means infinite lease time
                return null;
            }
#endif

            /// <summary>
            /// Returns the escaped value of the requested metadata name.
            /// </summary>
            string ITaskItem2.GetMetadataValueEscaped(string metadataName)
            {
                ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));

                string metadataValue = null;

                if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName))
                {
                    // FileUtilities.GetItemSpecModifier is expecting escaped data, which we assume we already are.
                    // Passing in a null for currentDirectory indicates we are already in the correct current directory
                    metadataValue = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(null, _escapedItemSpec, _escapedDefiningProject, metadataName, ref _fullPath);
                }
                else if (_customEscapedMetadata != null)
                {
                    _customEscapedMetadata.TryGetValue(metadataName, out metadataValue);
                }

                return metadataValue ?? String.Empty;
            }

            /// <summary>
            /// Sets the exact metadata value given to the metadata name requested.
            /// </summary>
            void ITaskItem2.SetMetadataValueLiteral(string metadataName, string metadataValue)
            {
                SetMetadata(metadataName, EscapingUtilities.Escape(metadataValue));
            }

            /// <summary>
            /// Returns a dictionary containing all metadata and their escaped forms.
            /// </summary>
            IDictionary ITaskItem2.CloneCustomMetadataEscaped()
            {
                IDictionary clonedDictionary = new Dictionary<string, string>(_customEscapedMetadata);
                return clonedDictionary;
            }

            public IEnumerable<KeyValuePair<string, string>> EnumerateMetadata()
            {
#if FEATURE_APPDOMAIN
                if (!AppDomain.CurrentDomain.IsDefaultAppDomain())
                {
                    return EnumerateMetadataEager();
                }
#endif

                return EnumerateMetadataLazy();
            }

            private IEnumerable<KeyValuePair<string, string>> EnumerateMetadataEager()
            {
                if (_customEscapedMetadata == null || _customEscapedMetadata.Count == 0)
                {
#if TASKHOST
                    // MSBuildTaskHost.dll compiles against .NET 3.5 which doesn't have Enumerable.Empty()
                    return new KeyValuePair<string, string>[0];
#else
                    return Enumerable.Empty<KeyValuePair<string, string>>();
#endif
                }

                var result = new KeyValuePair<string, string>[_customEscapedMetadata.Count];
                int index = 0;
                foreach (var kvp in _customEscapedMetadata)
                {
                    var unescaped = new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.UnescapeAll(kvp.Value));
                    result[index++] = unescaped;
                }

                return result;
            }

            private IEnumerable<KeyValuePair<string, string>> EnumerateMetadataLazy()
            {
                if (_customEscapedMetadata == null)
                {
                    yield break;
                }

                foreach (var kvp in _customEscapedMetadata)
                {
                    var unescaped = new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.UnescapeAll(kvp.Value));
                    yield return unescaped;
                }
            }

            public void ImportMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
            {
                foreach (KeyValuePair<string, string> kvp in metadata)
                {
                    SetMetadata(kvp.Key, kvp.Value);
                }
            }
        }
    }
}
