// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implementation of INodePacketTranslator.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
#if FEATURE_BINARY_SERIALIZATION
using System.Runtime.Serialization.Formatters.Binary;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using System.Globalization;
using System.Reflection;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is responsible for serializing and deserializing simple types to and
    /// from the byte streams used to communicate INodePacket-implementing classes.
    /// Each class implements a Translate method on INodePacket which takes this class
    /// as a parameter, and uses it to store and retrieve fields to the stream.
    /// </summary>
    static internal class NodePacketTranslator
    {
        /// <summary>
        /// Returns a read-only serializer.
        /// </summary>
        /// <returns>The serializer.</returns>
        static internal INodePacketTranslator GetReadTranslator(Stream stream, SharedReadBuffer buffer)
        {
            return new NodePacketReadTranslator(stream, buffer);
        }

        /// <summary>
        /// Returns a write-only serializer.
        /// </summary>
        /// <param name="stream">The stream containing data to serialize.</param>
        /// <returns>The serializer.</returns>
        static internal INodePacketTranslator GetWriteTranslator(Stream stream)
        {
            return new NodePacketWriteTranslator(stream);
        }

        /// <summary>
        /// Implementation of INodePacketTranslator for reading from a stream.
        /// </summary>
        private class NodePacketReadTranslator : INodePacketTranslator
        {
            /// <summary>
            /// The stream used as a source or destination for data.
            /// </summary>
            private Stream _packetStream;

            /// <summary>
            /// The binary reader used in read mode.
            /// </summary>
            private BinaryReader _reader;

            /// <summary>
            /// Constructs a serializer from the specified stream, operating in the designated mode.
            /// </summary>
            public NodePacketReadTranslator(Stream packetStream, SharedReadBuffer buffer)
            {
                _packetStream = packetStream;
                _reader = InterningBinaryReader.Create(packetStream, buffer);
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
                    ErrorUtilities.ThrowInternalError("Cannot get writer from reader.");
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

            /// <summary>
            /// Translates a boolean.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref bool value)
            {
                value = _reader.ReadBoolean();
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
                    byteArray = new byte[0];
                }
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
            /// Translates a list of T where T implements INodePacketTranslateable
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            /// <param name="factory">Factory to deserialize T</param>
            /// <typeparam name="T">TaskItem type</typeparam>
            public void Translate<T>(ref List<T> list, NodePacketValueFactory<T> factory) where T : INodePacketTranslatable
            {
                if (!TranslateNullable(list))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                list = new List<T>(count);

                for (int i = 0; i < count; i++)
                {
                    T value = default(T);

                    if (!TranslateNullable(value))
                    {
                        continue;
                    }

                    value = factory(this);
                    list.Add(value);
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
                value = new BuildEventContext
                    (
                    _reader.ReadInt32(),
                    _reader.ReadInt32(),
                    _reader.ReadInt32(),
                    _reader.ReadInt32(),
                    _reader.ReadInt32(),
                    _reader.ReadInt32()
                    );
            }

#endif

            /// <summary>
            /// Translates a CultureInfo
            /// </summary>
            /// <param name="value">The CultureInfo to translate</param>
            public void TranslateCulture(ref CultureInfo value)
            {
                string cultureName = _reader.ReadString();
                value = new CultureInfo(cultureName);
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
            {
                numericValue = _reader.ReadInt32();
                Type enumType = value.GetType();
                value = (T)Enum.ToObject(enumType, numericValue);
            }

#if FEATURE_BINARY_SERIALIZATION
            /// <summary>
            /// Translates a value using the .Net binary formatter.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void TranslateDotNet<T>(ref T value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                BinaryFormatter formatter = new BinaryFormatter();
                value = (T)formatter.Deserialize(_packetStream);
            }
#else
            public void TranslateBuildEventArgs(ref BuildEventArgs value)
            {
                var serializedBuildEventArgs = new SerializedBuildEventArgs();
                serializedBuildEventArgs.Translate(this);
                value = SerializedBuildEventArgs.ToEventArgs(serializedBuildEventArgs);
            }
#endif

            public void TranslateException(ref Exception value)
            {
#if FEATURE_BINARY_SERIALIZATION
                TranslateDotNet<Exception>(ref value);
#else
                if (!TranslateNullable(value))
                {
                    return;
                }
                var serializedException = new SerializedException();
                serializedException.Translate(this);
                value = SerializedException.ToException(serializedException);
#endif
            }


            /// <summary>
            /// Translates an object implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void Translate<T>(ref T value)
                where T : INodePacketTranslatable, new()
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value = new T();
                value.Translate(this);
            }

            /// <summary>
            /// Translates an object implementing INodePacketTranslatable which does not expose a
            /// public parameterless constructor.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            /// <param name="factory">The factory method used to instantiate values of type T.</param>
            public void Translate<T>(ref T value, NodePacketValueFactory<T> factory)
                where T : INodePacketTranslatable
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value = factory(this);
            }

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            public void TranslateArray<T>(ref T[] array)
                where T : INodePacketTranslatable, new()
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

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable requiring a factory to create.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            /// <param name="factory">The factory method used to instantiate values of type T.</param>
            public void TranslateArray<T>(ref T[] array, NodePacketValueFactory<T> factory)
                where T : INodePacketTranslatable
            {
                if (!TranslateNullable(array))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                array = new T[count];

                for (int i = 0; i < count; i++)
                {
                    array[i] = factory(this);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, string }.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new Dictionary<string, string>(count, comparer);

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    string value = null;
                    Translate(ref value);
                    dictionary[key] = value;
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, T }.  
            /// </summary>
            /// <typeparam name="T">The reference type for the values, which implements INodePacketTranslatable.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            public void TranslateDictionary<T>(ref Dictionary<string, T> dictionary, IEqualityComparer<string> comparer, NodePacketValueFactory<T> valueFactory)
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
                    dictionary[key] = value;
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
            /// </summary>
            /// <typeparam name="D">The reference type for the dictionary.</typeparam>
            /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            public void TranslateDictionary<D, T>(ref D dictionary, NodePacketValueFactory<T> valueFactory)
                where D : IDictionary<string, T>, new()
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
                    dictionary[key] = value;
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
            /// </summary>
            /// <typeparam name="D">The reference type for the dictionary.</typeparam>
            /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            /// <param name="dictionaryCreator">The delegate used to instantiate the dictionary.</param>
            public void TranslateDictionary<D, T>(ref D dictionary, NodePacketValueFactory<T> valueFactory, NodePacketDictionaryCreator<D> dictionaryCreator)
                where D : IDictionary<string, T>
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
                    dictionary[key] = value;
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
        }

        /// <summary>
        /// Implementation of INodePacketTranslator for writing to a stream.
        /// </summary>
        private class NodePacketWriteTranslator : INodePacketTranslator
        {
            /// <summary>
            /// The stream used as a source or destination for data.
            /// </summary>
            private Stream _packetStream;

            /// <summary>
            /// The binary writer used in write mode.
            /// </summary>
            private BinaryWriter _writer;

            /// <summary>
            /// Constructs a serializer from the specified stream, operating in the designated mode.
            /// </summary>
            /// <param name="packetStream">The stream serving as the source or destination of data.</param>
            public NodePacketWriteTranslator(Stream packetStream)
            {
                _packetStream = packetStream;
                _writer = new BinaryWriter(packetStream);
            }

            /// <summary>
            /// Gets the reader, if any.
            /// </summary>
            public BinaryReader Reader
            {
                get
                {
                    ErrorUtilities.ThrowInternalError("Cannot get reader from writer.");
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

            /// <summary>
            /// Translates a boolean.
            /// </summary>
            /// <param name="value">The value to be translated.</param>
            public void Translate(ref bool value)
            {
                _writer.Write(value);
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

                int count = 0;
                count = array.Length;
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

            /// <summary>
            /// Translates a list of T where T implements INodePacketTranslateable
            /// </summary>
            /// <param name="list">The list to be translated.</param>
            /// <param name="factory">factory to create type T</param>
            /// <typeparam name="T">A TaskItemType</typeparam>
            public void Translate<T>(ref List<T> list, NodePacketValueFactory<T> factory) where T : INodePacketTranslatable
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
                    Translate<T>(ref value, factory);
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
            {
                Type enumType = value.GetType();
                ErrorUtilities.VerifyThrow(enumType.GetTypeInfo().IsEnum, "Must pass an enum type.");

                _writer.Write(numericValue);
            }

#if FEATURE_BINARY_SERIALIZATION
            /// <summary>
            /// Translates a value using the .Net binary formatter.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void TranslateDotNet<T>(ref T value)
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(_packetStream, value);
            }
#else
            public void TranslateBuildEventArgs(ref BuildEventArgs value)
            {
                var serializedBuildEventArgs = SerializedBuildEventArgs.FromEventArgs(value);
                serializedBuildEventArgs.Translate(this);
            }
#endif

            public void TranslateException(ref Exception value)
            {
#if FEATURE_BINARY_SERIALIZATION
                TranslateDotNet<Exception>(ref value);
#else
                if (!TranslateNullable(value))
                {
                    return;
                }
                var serializedException = SerializedException.FromException(value);
                serializedException.Translate(this);
#endif
            }

            /// <summary>
            /// Translates an object implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            public void Translate<T>(ref T value)
                where T : INodePacketTranslatable, new()
            {
                if (!TranslateNullable(value))
                {
                    return;
                }

                value.Translate(this);
            }

            /// <summary>
            /// Translates an object implementing INodePacketTranslatable which does not expose a
            /// public parameterless constructor.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="value">The value to be translated.</param>
            /// <param name="factory">The factory method used to instantiate values of type T.</param>
            public void Translate<T>(ref T value, NodePacketValueFactory<T> factory)
                where T : INodePacketTranslatable
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
                if (!TranslateNullable(byteArray))
                {
                    return;
                }

                int count = byteArray.Length;
                _writer.Write(count);
                if (count > 0)
                {
                    _writer.Write(byteArray);
                }
            }

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            public void TranslateArray<T>(ref T[] array)
                where T : INodePacketTranslatable, new()
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

            /// <summary>
            /// Translates an array of objects implementing INodePacketTranslatable requiring a factory to create.
            /// </summary>
            /// <typeparam name="T">The reference type.</typeparam>
            /// <param name="array">The array to be translated.</param>
            /// <param name="factory">The factory method used to instantiate values of type T.</param>
            public void TranslateArray<T>(ref T[] array, NodePacketValueFactory<T> factory)
                where T : INodePacketTranslatable
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

            /// <summary>
            /// Translates a dictionary of { string, string }.
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer)
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

            /// <summary>
            /// Translates a dictionary of { string, T }.  
            /// </summary>
            /// <typeparam name="T">The reference type for the values, which implements INodePacketTranslatable.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            public void TranslateDictionary<T>(ref Dictionary<string, T> dictionary, IEqualityComparer<string> comparer, NodePacketValueFactory<T> valueFactory)
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
            /// </summary>
            /// <typeparam name="D">The reference type for the dictionary.</typeparam>
            /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            public void TranslateDictionary<D, T>(ref D dictionary, NodePacketValueFactory<T> valueFactory)
                where D : IDictionary<string, T>, new()
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
                }
            }

            /// <summary>
            /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
            /// </summary>
            /// <typeparam name="D">The reference type for the dictionary.</typeparam>
            /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="valueFactory">The factory used to instantiate values in the dictionary.</param>
            /// <param name="dictionaryCreator">The delegate used to instantiate the dictionary.</param>
            public void TranslateDictionary<D, T>(ref D dictionary, NodePacketValueFactory<T> valueFactory, NodePacketDictionaryCreator<D> dictionaryCreator)
                where D : IDictionary<string, T>
                where T : class, INodePacketTranslatable
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
                    Translate(ref value, valueFactory);
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
        }

#if !FEATURE_BINARY_SERIALIZATION
        public static bool IsSerializable(BuildEventArgs e)
        {
            var type = e.GetType();
            return SerializedBuildEventArgs.GetConstructor(type) != null;
        }        

        private class SerializedField : INodePacketTranslatable
        {
            public FieldInfo Field;
            public object FieldValue;

            TypeLoader _typeLoader;

            public SerializedField(TypeLoader typeLoader)
            {
                _typeLoader = typeLoader;
            }

            private static T ConvertOrDefault<T>(object value)
            {
                if (value == null)
                {
                    return default(T);
                }
                else
                {
                    return (T)value;
                }
            }

            public void Translate(INodePacketTranslator translator)
            {
                AssemblyLoadInfo assemblyLoadInfo = null;
                string declaringTypeFullName = null;
                string fieldName = null;
                if (translator.Mode == TranslationDirection.WriteToStream)
                {
                    assemblyLoadInfo = AssemblyLoadInfo.Create(null, AssemblyUtilities.GetAssemblyLocation(Field.DeclaringType.GetTypeInfo().Assembly));
                    declaringTypeFullName = Field.DeclaringType.FullName;
                    fieldName = Field.Name;
                }

                translator.Translate(ref assemblyLoadInfo, AssemblyLoadInfo.FactoryForTranslation);
                translator.Translate(ref declaringTypeFullName);
                translator.Translate(ref fieldName);

                if (translator.Mode == TranslationDirection.ReadFromStream)
                {
                    LoadedType loadedDclaringType = _typeLoader.Load(declaringTypeFullName, assemblyLoadInfo);
                    Type declaringType = loadedDclaringType.Type;
                    Field = declaringType.GetField(fieldName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                var fieldType = Field.FieldType;
                if (fieldType.GetTypeInfo().IsEnum)
                {
                    fieldType = Enum.GetUnderlyingType(fieldType);
                }

                if (fieldType == typeof(Boolean))
                {
                    bool val = ConvertOrDefault<bool>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(Char))
                {
                    ushort val = (ushort)ConvertOrDefault<char>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = (char)val;
                }
                else if (fieldType == typeof(SByte))
                {
                    byte val = unchecked((byte)ConvertOrDefault<SByte>(FieldValue));
                    translator.Translate(ref val);
                    FieldValue = unchecked((SByte)val);
                }
                else if (fieldType == typeof(Byte))
                {
                    byte val = ConvertOrDefault<byte>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(Int16))
                {
                    Int16 val = ConvertOrDefault<Int16>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(UInt16))
                {
                    UInt16 val = ConvertOrDefault<UInt16>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(Int32))
                {
                    Int32 val = ConvertOrDefault<Int32>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(UInt32))
                {
                    Int32 val = unchecked((Int32)ConvertOrDefault<UInt32>(FieldValue));
                    translator.Translate(ref val);
                    FieldValue = unchecked((UInt32)val);
                }
                //  Int64, UInt64, Single, Double, and Decimal not currently supported because INodePacketTranslator doesn't have Translate methods for these
                //  We can add them or work around this if needed
                else if (fieldType == typeof(DateTime))
                {
                    DateTime val = ConvertOrDefault<DateTime>(FieldValue);
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(String))
                {
                    String val = (String)FieldValue;
                    translator.Translate(ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(BuildEventContext))
                {
                    BuildEventContext val = (BuildEventContext)FieldValue;
                    SerializedBuildEventArgs.TranslateBuildEventContext(translator, ref val);
                    FieldValue = val;
                }
                else if (fieldType == typeof(IDictionary<string, string>))
                {
                    Dictionary<string, string> val = null;
                    if (FieldValue is Dictionary<string, string>)
                    {
                        val = (Dictionary<string, string>)FieldValue;
                    }
                    else if (FieldValue != null)
                    {
                        val = new Dictionary<string, string>((IDictionary<string, string>)FieldValue, MSBuildNameIgnoreCaseComparer.Default);
                    }
                    translator.TranslateDictionary(ref val, StringComparer.OrdinalIgnoreCase);
                    FieldValue = val;
                }
                else
                {
                    throw new NotSupportedException(Field.DeclaringType.FullName + "." + Field.Name + ": " + fieldType.ToString());
                }
            }
        }


        private static class FieldSerializer
        {
            public static List<SerializedField> SerializeFields(object obj, Func<FieldInfo, bool> fieldFilter)
            {
                List<SerializedField> ret = new List<SerializedField>();

                for (Type currentType = obj.GetType(); currentType != typeof(object); currentType = currentType.GetTypeInfo().BaseType)
                {
                    var fields = currentType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(fieldFilter);
                    foreach (var field in fields)
                    {
                        //  The TypeLoader in SerializedField is only needed for deserialization, so pass in null here
                        var serializedField = new SerializedField(null);
                        serializedField.Field = field;
                        serializedField.FieldValue = field.GetValue(obj);

                        ret.Add(serializedField);
                    }
                }

                return ret;
            }

            public static void DeserializeFields(object obj, List<SerializedField> serializedFields)
            {
                foreach (var serializedField in serializedFields)
                {
                    serializedField.Field.SetValue(obj, serializedField.FieldValue);
                }
            }
        }

        private class SerializedBuildEventArgs : INodePacketTranslatable
        {
            public AssemblyLoadInfo EventArgsAssembly;
            public string EventArgsTypeFullName;

            List<SerializedField> SerializedFields;

            public static SerializedBuildEventArgs FromEventArgs(BuildEventArgs e)
            {
                var ret = new SerializedBuildEventArgs();
                Type eventArgsType = e.GetType();

                ret.EventArgsAssembly = AssemblyLoadInfo.Create(null, AssemblyUtilities.GetAssemblyLocation(eventArgsType.GetTypeInfo().Assembly));
                ret.EventArgsTypeFullName = eventArgsType.FullName;

                //  Force LazyFormattedBuildEventArgs message to be materialized so we don't have to serialize the parameter object array
                string temp = e.Message;

                ret.SerializedFields = FieldSerializer.SerializeFields(e, fi =>
                {
                    if (fi.DeclaringType == typeof(LazyFormattedBuildEventArgs))
                    {
                        return false;
                    }
                    if ((fi.DeclaringType == typeof(ProjectStartedEventArgs) || fi.DeclaringType == typeof(TargetFinishedEventArgs))
                        && fi.FieldType == typeof(System.Collections.IEnumerable))
                    {
                        return false;
                    }
                    return true;
                });

                return ret;
            }

            public static BuildEventArgs ToEventArgs(SerializedBuildEventArgs serializedBuildEventArgs)
            {
                var typeLoader = new TypeLoader((t, o) => true);
                LoadedType loadedEventArgsType = typeLoader.Load(serializedBuildEventArgs.EventArgsTypeFullName, serializedBuildEventArgs.EventArgsAssembly);
                Type eventArgsType = loadedEventArgsType.Type;

                ConstructorInfo constructor = GetConstructor(eventArgsType);
                BuildEventArgs ret = (BuildEventArgs)constructor.Invoke(Array.Empty<object>());

                FieldSerializer.DeserializeFields(ret, serializedBuildEventArgs.SerializedFields);

                return ret;
            }

            public static void TranslateBuildEventContext(INodePacketTranslator translator, ref BuildEventContext context)
            {
                if (translator.TranslateNullable(context))
                {
                    int nodeId = 0;
                    int targetId = 0;
                    int projectContextId = 0;
                    int taskId = 0;
                    int projectInstanceId = 0;
                    int submissionId = 0;

                    if (translator.Mode == TranslationDirection.WriteToStream)
                    {
                        nodeId = context.NodeId;
                        targetId = context.TargetId;
                        projectContextId = context.ProjectContextId;
                        taskId = context.TaskId;
                        projectInstanceId = context.ProjectInstanceId;
                        submissionId = context.SubmissionId;
                    }

                    translator.Translate(ref nodeId);
                    translator.Translate(ref targetId);
                    translator.Translate(ref projectContextId);
                    translator.Translate(ref taskId);
                    translator.Translate(ref projectInstanceId);
                    translator.Translate(ref submissionId);

                    if (translator.Mode == TranslationDirection.ReadFromStream)
                    {
                        context = new BuildEventContext(submissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId);
                    }
                }
            }

            public static ConstructorInfo GetConstructor(Type type)
            {
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return constructors.FirstOrDefault(ci => ci.GetParameters().Length == 0);
            }

            public void Translate(INodePacketTranslator translator)
            {
                var typeLoader = new TypeLoader((t, o) => true);
                translator.Translate(ref EventArgsAssembly, AssemblyLoadInfo.FactoryForTranslation);
                translator.Translate(ref EventArgsTypeFullName);
                translator.Translate(ref SerializedFields, t =>
                {
                    var ret = new SerializedField(typeLoader);
                    ret.Translate(t);
                    return ret;
                });
            }
        }

        private class SerializedException : INodePacketTranslatable
        {
            public AssemblyLoadInfo ExceptionAssembly;
            public string TypeFullName;
            public string Message;
            public string StackTrace;
            public int HResult;
            public SerializedException InnerException;
            public string ExceptionToString;

            private static FieldInfo _exceptionStackTraceStringField;
            private static FieldInfo _exceptionHResultField;

            static SerializedException()
            {
                _exceptionStackTraceStringField = typeof(Exception).GetField("_stackTraceString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _exceptionHResultField = typeof(Exception).GetField("_HResult", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            public static SerializedException FromException(Exception ex)
            {
                var ret = new SerializedException();
                Type exceptionType = ex.GetType();
                ret.ExceptionAssembly = AssemblyLoadInfo.Create(null, AssemblyUtilities.GetAssemblyLocation(exceptionType.GetTypeInfo().Assembly));
                ret.TypeFullName = exceptionType.FullName;
                ret.Message = ex.Message;
                ret.StackTrace = ex.StackTrace;
                ret.HResult = ex.HResult;

                if (ex.InnerException != null)
                {
                    ret.InnerException = FromException(ex.InnerException);
                }

                ret.ExceptionToString = ex.ToString();

                return ret;
            }

            public static Exception ToException(SerializedException serializedException)
            {
                Exception innerException = null;
                if (serializedException.InnerException != null)
                {
                    innerException = ToException(serializedException.InnerException);
                }

                ConstructorInfo constructor = null;
                object[] constructorParameters = null;

                try
                {
                    var typeLoader = new TypeLoader((t, o) => true);

                    LoadedType loadedExceptionType = typeLoader.Load(serializedException.TypeFullName, serializedException.ExceptionAssembly);
                    Type exceptionType = loadedExceptionType.Type;

                    Type[] parameterTypes;
                    if (innerException == null)
                    {
                        parameterTypes = new[] { typeof(string) };
                        constructorParameters = new object[] { serializedException.Message };
                    }
                    else
                    {
                        parameterTypes = new[] { typeof(string), typeof(Exception) };
                        constructorParameters = new object[] { serializedException.Message, innerException };
                    }

                    constructor = exceptionType.GetConstructor(parameterTypes);
                }
                catch (FileLoadException e)
                {
                    CommunicationsUtilities.Trace($"Exception while attempting to deserialize an exception of type \"{serializedException.TypeFullName}\". Could not load from \"{serializedException.ExceptionAssembly.AssemblyLocation}\": {e}");
                }

                if (constructor == null)
                {
                    CommunicationsUtilities.Trace($"Could not find a constructor to deserialize an exception of type \"{serializedException.TypeFullName}\". Falling back to an exception that will look the same.");
                    return new FormattedException(serializedException.ExceptionToString);
                }

                Exception ret = (Exception) constructor.Invoke(constructorParameters);

                if (_exceptionStackTraceStringField != null)
                {
                    _exceptionStackTraceStringField.SetValue(ret, serializedException.StackTrace);
                }

                if (_exceptionHResultField != null)
                {
                    _exceptionHResultField.SetValue(ret, serializedException.HResult);
                }

                return ret;
            }

            public void Translate(INodePacketTranslator translator)
            {
                translator.Translate(ref ExceptionAssembly, AssemblyLoadInfo.FactoryForTranslation);
                translator.Translate(ref TypeFullName);
                translator.Translate(ref Message);
                translator.Translate(ref StackTrace);
                translator.Translate(ref HResult);
                translator.Translate(ref InnerException);
                translator.Translate(ref ExceptionToString);
            }

            private class FormattedException : Exception
            {
                string _formattedException;

                public FormattedException(string formattedException)
                {
                    _formattedException = formattedException;
                }

                public override string ToString()
                {
                    return _formattedException;
                }
            }
        }
#endif
                }
}