// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.BuildException;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is responsible for serializing and deserializing simple types to and
    /// from the byte streams used to communicate INodePacket-implementing classes.
    /// Each class implements a Translate method on INodePacket which takes this class
    /// as a parameter, and uses it to store and retrieve fields to the stream.
    /// </summary>
    internal static class BinaryTranslator
    {
        /// <summary>
        /// Presence of this key in the dictionary indicates that it was null.
        /// </summary>
        /// <remarks>
        /// This constant is needed for a workaround concerning serializing BuildResult with a version.
        /// </remarks>
        private const string SpecialKeyForDictionaryBeingNull = "=MSBUILDDICTIONARYWASNULL=";

#nullable enable
        /// <summary>
        /// Returns a read-only serializer.
        /// </summary>
        /// <returns>The serializer.</returns>
        internal static ITranslator GetReadTranslator(Stream stream, BinaryReaderFactory buffer)
        {
            return new BinaryReadTranslator(stream, buffer);
        }
#nullable disable

        /// <summary>
        /// Returns a write-only serializer.
        /// </summary>
        /// <param name="stream">The stream containing data to serialize.</param>
        /// <returns>The serializer.</returns>
        internal static ITranslator GetWriteTranslator(Stream stream)
        {
            return new BinaryWriteTranslator(stream);
        }

        /// <summary>
        /// Implementation of ITranslator for reading from a stream.
        /// </summary>
        private class BinaryReadTranslator : ITranslator
        {
            /// <summary>
            /// The intern reader used in an intern scope.
            /// </summary>
            private InterningReadTranslator _interner;

            /// <summary>
            /// The binary reader used in read mode.
            /// </summary>
            private BinaryReader _reader;

            /// <summary>
            /// Whether the caller has entered an intern scope.
            /// </summary>
            private bool _isInterning;

#nullable enable
            /// <summary>
            /// Constructs a serializer from the specified stream, operating in the designated mode.
            /// </summary>
            public BinaryReadTranslator(Stream packetStream, BinaryReaderFactory buffer)
            {
                _reader = buffer.Create(packetStream);
            }
#nullable disable

            /// <summary>
            /// Delegates the Dispose call the to the underlying BinaryReader.
            /// </summary>
            public void Dispose()
            {
                _reader.Close();
            }

            /// <summary>
            /// Gets the reader, if any.
            /// </summary>
            public BinaryReader Reader
            {
                get { return _reader; }
            }

            /// <summary>
            /// Gets the writer, if any.
            /// </summary>
            public BinaryWriter Writer
            {
                get
                {
                    EscapeHatches.ThrowInternalError("Cannot get writer from reader.");
                    return null;
                }
            }

            /// <summary>
            /// Returns the current serialization mode.
            /// </summary>
            public TranslationDirection Mode
            {
                [DebuggerStepThrough]
                get
                { return TranslationDirection.ReadFromStream; }
            }

            /// <inheritdoc/>
            public byte? NegotiatedPacketVersion { get; set; }

            /// <summary>
            /// Translates a boolean.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref bool value)
            {
                value = _reader.ReadBoolean();
            }

            /// <summary>
            /// Translates an <see langword="bool"/> array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref bool[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = _reader.ReadBoolean();
                }
            }

            /// <summary>
            /// Translates a byte.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref byte value)
            {
                value = _reader.ReadByte();
            }

            /// <summary>
            /// Translates a short.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref short value)
            {
                value = _reader.ReadInt16();
            }

            /// <summary>
            /// Translates an unsigned short.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref ushort value)
            {
                value = _reader.ReadUInt16();
            }

            /// <summary>
            /// Translates an integer.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref int value)
            {
                value = _reader.ReadInt32();
            }

            /// <inheritdoc/>
            public void Translate(ref uint unsignedInteger) => unsignedInteger = _reader.ReadUInt32();

            /// <summary>
            /// Translates a TaskHostParameters.
            /// </summary>
            /// <param name="value">The TaskHostParameters to be translated.</param>
            public void Translate(ref TaskHostParameters value)
            {
                string runtime = null;
                string architecture = null;
                string dotnetHostPath = null;
                string msBuildAssemblyPath = null;
                bool? isTaskHostFactory = null;

                Translate(ref runtime);
                Translate(ref architecture);
                Translate(ref dotnetHostPath);
                Translate(ref msBuildAssemblyPath);

                bool hasTaskHostFactory = _reader.ReadBoolean();
                if (hasTaskHostFactory)
                {
                    isTaskHostFactory = _reader.ReadBoolean();
                }

                value = new TaskHostParameters(
                    runtime: runtime,
                    architecture: architecture,
                    dotnetHostPath: dotnetHostPath,
                    msBuildAssemblyPath: msBuildAssemblyPath,
                    taskHostFactoryExplicitlyRequested: isTaskHostFactory);
            }

            /// <summary>
            /// Translates an <see langword="int"/> array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref int[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new int[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = _reader.ReadInt32();
                }
            }

            /// <summary>
            /// Translates a long.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref long value)
            {
                value = _reader.ReadInt64();
            }

            /// <summary>
            /// Translates a double.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref double value)
            {
                value = _reader.ReadDouble();
            }

            /// <summary>
            /// Translates a string.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref string value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value = _reader.ReadString();
            }

            /// <summary>
            /// Translates a byte array
            /// </summary>
            /// <param name="byteArray">The array to be translated</param>
            public void Translate(ref byte[] byteArray)
            {
                if (!TranslateNullable(byteArray))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                if (count > 0)
                {
                    byteArray = _reader.ReadBytes(count);
                }
                else
                {
#pragma warning disable CA1825 // Avoid zero-length array allocations
                    byteArray = new byte[0];
#pragma warning restore CA1825 // Avoid zero-length array allocations
                }
            }

            /// <summary>
            /// Translates a byte array
            /// </summary>
            /// <param name="byteArray">The array to be translated.</param>
            /// <param name="length">The length of array which will be used in translation. This parameter is not used when reading</param>
            public void Translate(ref byte[] byteArray, ref int length)
            {
                Translate(ref byteArray);
                length = byteArray.Length;
            }

            /// <summary>
            /// Translates a string array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref string[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new string[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = _reader.ReadString();
                }
            }

            /// <inheritdoc />
            public void Translate(ref HashSet<string> set)
            {
                if (!TranslateNullable(set))
                {
                    return;
                }

                int count = _reader.ReadInt32();
#if NET472_OR_GREATER || NET9_0_OR_GREATER
                set = new HashSet<string>(count);
#else
                set = new HashSet<string>();
#endif

                for (int i = 0; i < count; i++)
                {
                    set.Add(_reader.ReadString());
                }
            }

            /// <summary>
            /// Translates a list of strings
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            public void Translate(ref List<string> list)
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                list = new List<string>(count);

                for (int i = 0; i < count; i++)
                {
                    list.Add(_reader.ReadString());
                }
            }

            /// <summary>
            /// Translates a list of T using an <see cref="ObjectTranslator{T}"/>
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            /// <param name="objectTranslator">The translator to use for the items in the list</param>
            /// <typeparam name="T">TaskItem type</typeparam>
            public void Translate<T>(ref List<T> list, ObjectTranslator<T> objectTranslator)
            {
                IList<T> listAsInterface = list;
                Translate(ref listAsInterface, objectTranslator, count => new List<T>(count));
                list = (List<T>)listAsInterface;
            }

            /// <inheritdoc/>
            public void Translate<T>(ref List<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            {
                IList<T> listAsInterface = list;
                Translate(ref listAsInterface, objectTranslator, valueFactory, count => new List<T>(count));
                list = (List<T>)listAsInterface;
            }

            public void Translate<T, L>(ref IList<T> list, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                list = collectionFactory(count);

                for (int i = 0; i < count; i++)
                {
                    T value = default(T);

                    objectTranslator(this, ref value);
                    list.Add(value);
                }
            }

            public void Translate<T, L>(ref IList<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                list = collectionFactory(count);

                for (int i = 0; i < count; i++)
                {
                    T value = default(T);

                    objectTranslator(this, valueFactory, ref value);
                    list.Add(value);
                }
            }

            /// <summary>
            /// Translates a collection of T into the specified type using an <see cref="ObjectTranslator{T}"/> and <see cref="NodePacketCollectionCreator{L}"/>
            /// </summary>
            /// <param name="collection">The collection to be translated.</param>
            /// <param name="objectTranslator">The translator to use for the values in the collection.</param>
            /// <param name="collectionFactory">The factory to create the ICollection.</param>
            /// <typeparam name="T">The type contained in the collection.</typeparam>
            /// <typeparam name="L">The type of collection to be created.</typeparam>
            public void Translate<T, L>(ref ICollection<T> collection, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : ICollection<T>
            {
                if (!TranslateNullable(collection))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                collection = collectionFactory(count);

                for (int i = 0; i < count; i++)
                {
                    T value = default(T);
                    objectTranslator(this, ref value);
                    collection.Add(value);
                }
            }

            /// <summary>
            /// Translates a DateTime.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref DateTime value)
            {
                DateTimeKind kind = DateTimeKind.Unspecified;
                TranslateEnum<DateTimeKind>(ref kind, 0);
                value = new DateTime(_reader.ReadInt64(), kind);
            }

            /// <summary>
            /// Translates a TimeSpan.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref TimeSpan value)
            {
                long ticks = 0;
                Translate(ref ticks);
                value = new System.TimeSpan(ticks);
            }

            // MSBuildTaskHost is based on CLR 3.5, which does not have the 6-parameter constructor for BuildEventContext.
            // However, it also does not ever need to translate BuildEventContexts, so it should be perfectly safe to
            // compile this method out of that assembly.
#if !CLR2COMPATIBILITY

            /// <summary>
            /// Translates a BuildEventContext
            /// </summary>
            /// <remarks>
            /// This method exists only because there is no serialization method built into the BuildEventContext
            /// class, and it lives in Framework and we don't want to add a public method to it.
            /// </remarks>
            /// <param name="value">The context to be translated.</param>
            public void Translate(ref BuildEventContext value)
            {
                int submissionId = _reader.ReadInt32();
                int nodeId = _reader.ReadInt32();
                int evaluationId = _reader.ReadInt32();
                int projectInstanceId = _reader.ReadInt32();
                int projectContextId = _reader.ReadInt32();
                int targetId = _reader.ReadInt32();
                int taskId = _reader.ReadInt32();

                value = BuildEventContext.CreateInitial(submissionId, nodeId)
                    .WithEvaluationId(evaluationId)
                    .WithProjectInstanceId(projectInstanceId)
                    .WithProjectContextId(projectContextId)
                    .WithTargetId(targetId)
                    .WithTaskId(taskId);
            }
#endif

            /// <summary>
            /// Translates a CultureInfo
            /// </summary>
            /// <param name="value">The CultureInfo to translate</param>
            public void TranslateCulture(ref CultureInfo value)
            {
                string cultureName = _reader.ReadString();

#if CLR2COMPATIBILITY
                // It may be that some culture codes are accepted on later .net framework versions
                // but not on the older 3.5 or 2.0. Fallbacks are required in this case to prevent
                // exceptions
                value = LoadCultureWithFallback(cultureName);
#else
                value = new CultureInfo(cultureName);
#endif
            }

#if CLR2COMPATIBILITY
            private static CultureInfo LoadCultureWithFallback(string cultureName)
            {
                CultureInfo cultureInfo;

                return TryLoadCulture(cultureName, out cultureInfo) ? cultureInfo : CultureInfo.CurrentCulture;
            }

            private static bool TryLoadCulture(string cultureName, out CultureInfo cultureInfo)
            {
                try
                {
                    cultureInfo = new CultureInfo(cultureName);
                    return true;
                }
                catch
                {
                    cultureInfo = null;
                    return false;
                }
            }
#endif

            /// <summary>
            /// Translates an enumeration.
            /// </summary>
            /// <typeparam name="T">The enumeration type.</typeparam>
            /// <param name="value">The enumeration instance to be translated.</param>
            /// <param name="numericValue">The enumeration value as an integer.</param>
            /// <remarks>This is a bit ugly, but it doesn't seem like a nice method signature is possible because
            /// you can't pass the enum type as a reference and constrain the generic parameter to Enum.  Nor
            /// can you simply pass as ref Enum, because an enum instance doesn't match that function signature.
            /// Finally, converting the enum to an int assumes that we always want to transport enums as ints.  This
            /// works in all of our current cases, but certainly isn't perfectly generic.</remarks>
            public void TranslateEnum<T>(ref T value, int numericValue)
                where T : struct, Enum
            {
                numericValue = _reader.ReadInt32();
                Type enumType = value.GetType();
                value = (T)Enum.ToObject(enumType, numericValue);
            }

            public void TranslateException(ref Exception value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value = BuildExceptionBase.ReadExceptionFromTranslator(this);
            }


            /// <summary>
            /// Translates an object implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void Translate<T>(ref T value)
                where T : ITranslatable, new()
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value = new T();
                value.Translate(this);
            }

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            public void TranslateArray<T>(ref T[] array)
                where T : ITranslatable, new()
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new T[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = new T();
                    array[i].Translate(this);
                }
            }

            /// <inheritdoc/>
            public void TranslateArray<T>(ref T[] array, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new T[count];

                for (int i = 0; i < count; i++)
                {
                    objectTranslator(this, valueFactory, ref array[i]);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, string }.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer)
            {
                IDictionary<string, string> copy = dictionary;

                TranslateDictionary(
                    ref copy,
                    count => new Dictionary<string, string>(count, comparer));

                dictionary = (Dictionary<string, string>)copy;
            }

            /// <summary>
            /// Translates a dictionary of { string, string } with additional entries. The dictionary might be null despite being populated.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            /// <param name="additionalEntries">Additional entries to be translated</param>
            /// <param name="additionalEntriesKeys">Additional entries keys</param>
            /// <remarks>
            /// This overload is needed for a workaround concerning serializing BuildResult with a version.
            /// It deserializes additional entries together with the main dictionary.
            /// </remarks>
            public void TranslateDictionary(ref IDictionary<string, string> dictionary, IEqualityComparer<string> comparer, ref Dictionary<string, string> additionalEntries, HashSet<string> additionalEntriesKeys)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new Dictionary<string, string>(count, comparer);
                additionalEntries = new();

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    string value = null;
                    Translate(ref value);
                    if (additionalEntriesKeys.Contains(key))
                    {
                        additionalEntries[key] = value;
                    }
                    else if (comparer.Equals(key, SpecialKeyForDictionaryBeingNull))
                    {
                        // Presence of special key SpecialKeyForDictionaryBeingNull indicates that the dictionary was null.
                        dictionary = null;

                        // If the dictionary is null, we should have only two keys: SpecialKeyForDictionaryBeingNull, SpecialKeyForVersion
                        Debug.Assert(count == 2);
                    }
                    else if (dictionary is not null)
                    {
                        dictionary[key] = value;
                    }
                }
            }

            public void TranslateDictionary(ref IDictionary<string, string> dictionary, NodePacketCollectionCreator<IDictionary<string, string>> dictionaryCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = dictionaryCreator(count);

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    string value = null;
                    Translate(ref value);
                    dictionary[key] = value;
                }
            }

            public void TranslateDictionary<K, V>(
                ref IDictionary<K, V> dictionary,
                ObjectTranslator<K> keyTranslator,
                ObjectTranslator<V> valueTranslator,
                NodePacketCollectionCreator<IDictionary<K, V>> dictionaryCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = dictionaryCreator(count);

                for (int i = 0; i < count; i++)
                {
                    K key = default(K);
                    keyTranslator(this, ref key);
                    V value = default(V);
                    valueTranslator(this, ref value);
                    dictionary[key] = value;
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<K, V>(
                ref IDictionary<K, V> dictionary,
                ObjectTranslator<K> keyTranslator,
                ObjectTranslatorWithValueFactory<V> valueTranslator,
                NodePacketValueFactory<V> valueFactory,
                NodePacketCollectionCreator<IDictionary<K, V>> dictionaryCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = dictionaryCreator(count);

                for (int i = 0; i < count; i++)
                {
                    K key = default(K);
                    keyTranslator(this, ref key);
                    V value = default(V);
                    valueTranslator(this, valueFactory, ref value);
                    dictionary[key] = value;
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<T>(ref Dictionary<string, T> dictionary, IEqualityComparer<string> comparer, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new Dictionary<string, T>(count, comparer);

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    T value = null;
                    objectTranslator(this, valueFactory, ref value);
                    dictionary[key] = value;
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
                where D : IDictionary<string, T>, new()
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new D();

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    T value = null;
                    objectTranslator(this, valueFactory, ref value);
                    dictionary[key] = value;
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<D> dictionaryCreator)
                where D : IDictionary<string, T>
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = dictionaryCreator(count);

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    T value = null;
                    objectTranslator(this, valueFactory, ref value);
                    dictionary[key] = value;
                }
            }

            public void TranslateDictionary(ref Dictionary<string, DateTime> dictionary, StringComparer comparer)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new(count, comparer);
                string key = string.Empty;
                DateTime val = DateTime.MinValue;
                for (int i = 0; i < count; i++)
                {
                    Translate(ref key);
                    Translate(ref val);
                    dictionary.Add(key, val);
                }
            }

            /// <summary>
            /// Reads in the boolean which says if this object is null or not.
            /// </summary>
            /// <typeparam name="T">The type of object to test.</typeparam>
            /// <returns>True if the object should be read, false otherwise.</returns>
            public bool TranslateNullable<T>(T value)
            {
                bool haveRef = _reader.ReadBoolean();
                return haveRef;
            }

            public void WithInterning(IEqualityComparer<string> comparer, int initialCapacity, Action<ITranslator> internBlock)
            {
                if (_isInterning)
                {
                    throw new InvalidOperationException("Cannot enter recursive intern block.");
                }

                _isInterning = true;

                // Deserialize the intern header before entering the intern scope.
                _interner ??= new InterningReadTranslator(this);
                _interner.Translate(this);

                // No other setup is needed since we can parse the packet directly from the stream.
                internBlock(this);

                _isInterning = false;
            }

            public void Intern(ref string str, bool nullable = true)
            {
                if (!_isInterning)
                {
                    Translate(ref str);
                    return;
                }

                if (nullable && !TranslateNullable(string.Empty))
                {
                    str = null;
                    return;
                }

                str = _interner.Read();
            }

            public void Intern(ref string[] array)
            {
                if (!_isInterning)
                {
                    Translate(ref array);
                    return;
                }

                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new string[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = _interner.Read();
                }
            }

            public void InternPath(ref string str, bool nullable = true)
            {
                if (!_isInterning)
                {
                    Translate(ref str);
                    return;
                }

                if (nullable && !TranslateNullable(string.Empty))
                {
                    str = null;
                    return;
                }

                str = _interner.ReadPath();
            }
        }

        /// <summary>
        /// Implementation of ITranslator for writing to a stream.
        /// </summary>
        private class BinaryWriteTranslator : ITranslator
        {
            /// <summary>
            /// The binary writer used in write mode.
            /// </summary>
            private BinaryWriter _writer;

            /// <summary>
            /// The intern writer used in an intern scope.
            /// This must be lazily instantiated since the interner has its own internal write translator, and
            /// would otherwise go into a recursive loop on initalization.
            /// </summary>
            private InterningWriteTranslator _interner;

            /// <summary>
            /// Whether the caller has entered an intern scope.
            /// </summary>
            private bool _isInterning;

            /// <summary>
            /// Constructs a serializer from the specified stream, operating in the designated mode.
            /// </summary>
            /// <param name="packetStream">The stream serving as the source or destination of data.</param>
            public BinaryWriteTranslator(Stream packetStream)
            {
                _writer = new BinaryWriter(packetStream);
            }

            /// <summary>
            /// Delegates the Dispose call the to the underlying BinaryWriter.
            /// </summary>
            public void Dispose()
            {
                _writer.Close();
            }

            /// <summary>
            /// Gets the reader, if any.
            /// </summary>
            public BinaryReader Reader
            {
                get
                {
                    EscapeHatches.ThrowInternalError("Cannot get reader from writer.");
                    return null;
                }
            }

            /// <summary>
            /// Gets the writer, if any.
            /// </summary>
            public BinaryWriter Writer
            {
                get { return _writer; }
            }

            /// <summary>
            /// Returns the current serialization mode.
            /// </summary>
            public TranslationDirection Mode
            {
                [DebuggerStepThrough]
                get
                { return TranslationDirection.WriteToStream; }
            }

            /// <inheritdoc/>
            public byte? NegotiatedPacketVersion { get; set; }

            /// <summary>
            /// Translates a boolean.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref bool value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates an <see langword="bool"/> array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref bool[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    _writer.Write(array[i]);
                }
            }

            /// <summary>
            /// Translates a byte.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref byte value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates a short.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref short value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates an unsigned short.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref ushort value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates an integer.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref int value)
            {
                _writer.Write(value);
            }

            /// <inheritdoc/>
            public void Translate(ref uint unsignedInteger) => _writer.Write(unsignedInteger);

            /// <summary>
            /// Translates an <see langword="int"/> array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref int[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    _writer.Write(array[i]);
                }
            }

            /// <summary>
            /// Translates a long.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref long value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates a double.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref double value)
            {
                _writer.Write(value);
            }

            /// <summary>
            /// Translates a TaskHostParameters.
            /// </summary>
            /// <param name="value">The TaskHostParameters to be translated.</param>
            public void Translate(ref TaskHostParameters value)
            {
                string runtime = value.Runtime;
                string architecture = value.Architecture;
                string dotnetHostPath = value.DotnetHostPath;
                string msBuildAssemblyPath = value.MSBuildAssemblyPath;

                Translate(ref runtime);
                Translate(ref architecture);
                Translate(ref dotnetHostPath);
                Translate(ref msBuildAssemblyPath);

                bool hasTaskHostFactory = value.TaskHostFactoryExplicitlyRequested.HasValue;
                _writer.Write(hasTaskHostFactory);
                if (hasTaskHostFactory)
                {
                    _writer.Write(value.TaskHostFactoryExplicitlyRequested.Value);
                }
            }

            /// <summary>
            /// Translates a string.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref string value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                _writer.Write(value);
            }

            /// <summary>
            /// Translates a string array.
            /// </summary>
            /// <param name="array">The array to be translated.</param>
            public void Translate(ref string[] array)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    _writer.Write(array[i]);
                }
            }

            /// <summary>
            /// Translates a list of strings
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            public void Translate(ref List<string> list)
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = list.Count;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    _writer.Write(list[i]);
                }
            }

            /// <inheritdoc />
            public void Translate(ref HashSet<string> set)
            {
                if (!TranslateNullable(set))
                {
                    return;
                }

                int count = set.Count;
                _writer.Write(count);

                foreach (var item in set)
                {
                    _writer.Write(item);
                }
            }

            /// <summary>
            /// Translates a list of T using an <see cref="ObjectTranslator{T}"/>
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            /// <param name="objectTranslator">The translator to use for the items in the list</param>
            /// <typeparam name="T">A TaskItemType</typeparam>
            public void Translate<T>(ref List<T> list, ObjectTranslator<T> objectTranslator)
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = list.Count;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    T value = list[i];
                    objectTranslator(this, ref value);
                }
            }

            /// <inheritdoc/>
            public void Translate<T>(ref List<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = list.Count;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    T value = list[i];
                    objectTranslator(this, valueFactory, ref value);
                }
            }

            /// <summary>
            /// Translates a list of T using an <see cref="ObjectTranslator{T}"/>
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            /// <param name="objectTranslator">The translator to use for the items in the list</param>
            /// <param name="collectionFactory">factory to create the IList</param>
            /// <typeparam name="T">A TaskItemType</typeparam>
            /// <typeparam name="L">IList subtype</typeparam>
            public void Translate<T, L>(ref IList<T> list, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = list.Count;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    T value = list[i];
                    objectTranslator(this, ref value);
                }
            }

            /// <inheritdoc/>
            public void Translate<T, L>(ref IList<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = list.Count;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    T value = list[i];
                    objectTranslator(this, valueFactory, ref value);
                }
            }

            /// <summary>
            /// Translates a collection of T into the specified type using an <see cref="ObjectTranslator{T}"/> and <see cref="NodePacketCollectionCreator{L}"/>
            /// </summary>
            /// <param name="collection">The collection to be translated.</param>
            /// <param name="objectTranslator">The translator to use for the values in the collection.</param>
            /// <param name="collectionFactory">The factory to create the ICollection.</param>
            /// <typeparam name="T">The type contained in the collection.</typeparam>
            /// <typeparam name="L">The type of collection to be created.</typeparam>
            public void Translate<T, L>(ref ICollection<T> collection, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : ICollection<T>
            {
                if (!TranslateNullable(collection))
                {
                    return;
                }

                _writer.Write(collection.Count);

                foreach (T item in collection)
                {
                    T value = item;
                    objectTranslator(this, ref value);
                }
            }

            /// <summary>
            /// Translates a DateTime.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref DateTime value)
            {
                DateTimeKind kind = value.Kind;
                TranslateEnum<DateTimeKind>(ref kind, (int)kind);
                _writer.Write(value.Ticks);
            }

            /// <summary>
            /// Translates a TimeSpan.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref TimeSpan value)
            {
                _writer.Write(value.Ticks);
            }

            // MSBuildTaskHost is based on CLR 3.5, which does not have the 6-parameter constructor for BuildEventContext.
            // However, it also does not ever need to translate BuildEventContexts, so it should be perfectly safe to
            // compile this method out of that assembly.
#if !CLR2COMPATIBILITY

            /// <summary>
            /// Translates a BuildEventContext
            /// </summary>
            /// <remarks>
            /// This method exists only because there is no serialization method built into the BuildEventContext
            /// class, and it lives in Framework and we don't want to add a public method to it.
            /// </remarks>
            /// <param name="value">The context to be translated.</param>
            public void Translate(ref BuildEventContext value)
            {
                _writer.Write(value.SubmissionId);
                _writer.Write(value.NodeId);
                _writer.Write(value.EvaluationId);
                _writer.Write(value.ProjectInstanceId);
                _writer.Write(value.ProjectContextId);
                _writer.Write(value.TargetId);
                _writer.Write(value.TaskId);
            }
#endif

            /// <summary>
            /// Translates a CultureInfo
            /// </summary>
            /// <param name="value">The CultureInfo</param>
            public void TranslateCulture(ref CultureInfo value)
            {
                _writer.Write(value.Name);
            }

            /// <summary>
            /// Translates an enumeration.
            /// </summary>
            /// <typeparam name="T">The enumeration type.</typeparam>
            /// <param name="value">The enumeration instance to be translated.</param>
            /// <param name="numericValue">The enumeration value as an integer.</param>
            /// <remarks>This is a bit ugly, but it doesn't seem like a nice method signature is possible because
            /// you can't pass the enum type as a reference and constrain the generic parameter to Enum.  Nor
            /// can you simply pass as ref Enum, because an enum instance doesn't match that function signature.
            /// Finally, converting the enum to an int assumes that we always want to transport enums as ints.  This
            /// works in all of our current cases, but certainly isn't perfectly generic.</remarks>
            public void TranslateEnum<T>(ref T value, int numericValue)
                where T : struct, Enum
            {
                _writer.Write(numericValue);
            }

            public void TranslateException(ref Exception value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                BuildExceptionBase.WriteExceptionToTranslator(this, value);
            }

            /// <summary>
            /// Translates an object implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void Translate<T>(ref T value)
                where T : ITranslatable, new()
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value.Translate(this);
            }

            /// <summary>
            /// Translates a byte array
            /// </summary>
            /// <param name="byteArray">The byte array to be translated</param>
            public void Translate(ref byte[] byteArray)
            {
                var length = byteArray?.Length ?? 0;
                Translate(ref byteArray, ref length);
            }

            /// <summary>
            /// Translates a byte array
            /// </summary>
            /// <param name="byteArray">The array to be translated.</param>
            /// <param name="length">The length of array which will be used in translation</param>
            public void Translate(ref byte[] byteArray, ref int length)
            {
                if (!TranslateNullable(byteArray))
                {
                    return;
                }

                _writer.Write(length);
                if (length > 0)
                {
                    _writer.Write(byteArray, 0, length);
                }
            }

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            public void TranslateArray<T>(ref T[] array)
                where T : ITranslatable, new()
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    array[i].Translate(this);
                }
            }

            /// <inheritdoc/>
            public void TranslateArray<T>(ref T[] array, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                _writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    objectTranslator(this, valueFactory, ref array[i]);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, string }.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer)
            {
                IDictionary<string, string> copy = dictionary;
                TranslateDictionary(ref copy, (NodePacketCollectionCreator<IDictionary<string, string>>)null);
            }

            /// <summary>
            /// Translates a dictionary of { string, string } adding additional entries.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            /// <param name="additionalEntries">Additional entries to be translated.</param>
            /// <param name="additionalEntriesKeys">Additional entries keys.</param>
            /// <remarks>
            /// This overload is needed for a workaround concerning serializing BuildResult with a version.
            /// It serializes additional entries together with the main dictionary.
            /// </remarks>
            public void TranslateDictionary(ref IDictionary<string, string> dictionary, IEqualityComparer<string> comparer, ref Dictionary<string, string> additionalEntries, HashSet<string> additionalEntriesKeys)
            {
                // Translate whether object is null
                if ((dictionary is null) && ((additionalEntries is null) || (additionalEntries.Count == 0)))
                {
                    _writer.Write(false);
                    return;
                }
                else
                {
                    // Translate that object is not null
                    _writer.Write(true);
                }

                // Writing a dictionary, additional entries and special key if dictionary was null. We need the special key for distinguishing whether the initial dictionary was null or empty.
                int count = (dictionary is null ? 1 : 0) +
                            (additionalEntries is null ? 0 : additionalEntries.Count) +
                            (dictionary is null ? 0 : dictionary.Count);

                _writer.Write(count);

                // If the dictionary was null, serialize a special key SpecialKeyForDictionaryBeingNull.
                if (dictionary is null)
                {
                    string key = SpecialKeyForDictionaryBeingNull;
                    Translate(ref key);
                    string value = string.Empty;
                    Translate(ref value);
                }

                // Serialize additional entries
                if (additionalEntries is not null)
                {
                    foreach (KeyValuePair<string, string> pair in additionalEntries)
                    {
                        string key = pair.Key;
                        Translate(ref key);
                        string value = pair.Value;
                        Translate(ref value);
                    }
                }

                // Serialize dictionary
                if (dictionary is not null)
                {
                    foreach (KeyValuePair<string, string> pair in dictionary)
                    {
                        string key = pair.Key;
                        Translate(ref key);
                        string value = pair.Value;
                        Translate(ref value);
                    }
                }
            }

            public void TranslateDictionary(ref IDictionary<string, string> dictionary, NodePacketCollectionCreator<IDictionary<string, string>> dictionaryCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<string, string> pair in dictionary)
                {
                    string key = pair.Key;
                    Translate(ref key);
                    string value = pair.Value;
                    Translate(ref value);
                }
            }

            public void TranslateDictionary<K, V>(
                ref IDictionary<K, V> dictionary,
                ObjectTranslator<K> keyTranslator,
                ObjectTranslator<V> valueTranslator,
                NodePacketCollectionCreator<IDictionary<K, V>> collectionCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<K, V> pair in dictionary)
                {
                    K key = pair.Key;
                    keyTranslator(this, ref key);
                    V value = pair.Value;
                    valueTranslator(this, ref value);
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<K, V>(
                ref IDictionary<K, V> dictionary,
                ObjectTranslator<K> keyTranslator,
                ObjectTranslatorWithValueFactory<V> valueTranslator,
                NodePacketValueFactory<V> valueFactory,
                NodePacketCollectionCreator<IDictionary<K, V>> collectionCreator)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<K, V> pair in dictionary)
                {
                    K key = pair.Key;
                    keyTranslator(this, ref key);
                    V value = pair.Value;
                    valueTranslator(this, valueFactory, ref value);
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<T>(ref Dictionary<string, T> dictionary, IEqualityComparer<string> comparer, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<string, T> pair in dictionary)
                {
                    string key = pair.Key;
                    Translate(ref key);
                    T value = pair.Value;
                    objectTranslator(this, valueFactory, ref value);
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
                where D : IDictionary<string, T>, new()
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<string, T> pair in dictionary)
                {
                    string key = pair.Key;
                    Translate(ref key);
                    T value = pair.Value;
                    objectTranslator(this, valueFactory, ref value);
                }
            }

            /// <inheritdoc/>
            public void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<D> dictionaryCreator)
                where D : IDictionary<string, T>
                where T : class
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<string, T> pair in dictionary)
                {
                    string key = pair.Key;
                    Translate(ref key);
                    T value = pair.Value;
                    objectTranslator(this, valueFactory, ref value);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, DateTime }.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">Key comparer</param>
            public void TranslateDictionary(ref Dictionary<string, DateTime> dictionary, StringComparer comparer)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);
                foreach (KeyValuePair<string, DateTime> kvp in dictionary)
                {
                    string key = kvp.Key;
                    DateTime val = kvp.Value;
                    Translate(ref key);
                    Translate(ref val);
                }
            }

            /// <summary>
            /// Writes out the boolean which says if this object is null or not.
            /// </summary>
            /// <param name="value">The object to test.</param>
            /// <typeparam name="T">The type of object to test.</typeparam>
            /// <returns>True if the object should be written, false otherwise.</returns>
            public bool TranslateNullable<T>(T value)
            {
                bool haveRef = (value != null);
                _writer.Write(haveRef);
                return haveRef;
            }

            public void WithInterning(IEqualityComparer<string> comparer, int initialCapacity, Action<ITranslator> internBlock)
            {
                if (_isInterning)
                {
                    throw new InvalidOperationException("Cannot enter recursive intern block.");
                }

                // Every new scope requires the interner's state to be reset.
                _interner ??= new InterningWriteTranslator();
                _interner.Setup(comparer, initialCapacity);

                // Temporaily swap our writer with the interner.
                // This forwards all writes to this translator into the interning buffer, so that any non-interned
                // writes which are interleaved will be in the correct order.
                BinaryWriter streamWriter = _writer;
                _writer = _interner.Writer;
                _isInterning = true;

                try
                {
                    internBlock(this);
                }
                finally
                {
                    _writer = streamWriter;
                    _isInterning = false;
                }

                // Write the interned buffer into the real output stream.
                _interner.Translate(this);
            }

            public void Intern(ref string str, bool nullable = true)
            {
                if (!_isInterning)
                {
                    Translate(ref str);
                    return;
                }

                if (nullable && !TranslateNullable(str))
                {
                    return;
                }

                _interner.Intern(str);
            }

            public void Intern(ref string[] array)
            {
                if (!_isInterning)
                {
                    Translate(ref array);
                    return;
                }

                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = array.Length;
                Translate(ref count);

                for (int i = 0; i < count; i++)
                {
                    _interner.Intern(array[i]);
                }
            }

            public void InternPath(ref string str, bool nullable = true)
            {
                if (!_isInterning)
                {
                    Translate(ref str);
                    return;
                }

                if (nullable && !TranslateNullable(str))
                {
                    return;
                }

                _interner.InternPath(str);
            }
        }
    }
}
