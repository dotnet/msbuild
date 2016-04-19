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
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Globalization;

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
        /// <param name="stream">The stream containing data to deserialize.</param>
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
            /// <param name="packetStream">The stream serving as the source or destination of data.</param>
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
            /// Translates a dictionary of type { string, List {string} }
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionaryList(ref Dictionary<string, List<string>> dictionary, IEqualityComparer<string> comparer)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = _reader.ReadInt32();
                dictionary = new Dictionary<string, List<string>>(count, comparer);

                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    Translate(ref key);
                    List<string> value = null;
                    Translate(ref value);
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
                ErrorUtilities.VerifyThrow(enumType.IsEnum, "Must pass an enum type.");

                _writer.Write(numericValue);
            }

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
            /// Translates a dictionary of type { string, List {string} }
            /// </summary>
            /// <param name="dictionary">The dictionary to be translated.</param>
            /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
            public void TranslateDictionaryList(ref Dictionary<string, List<string>> dictionary, IEqualityComparer<string> comparer)
            {
                if (!TranslateNullable(dictionary))
                {
                    return;
                }

                int count = dictionary.Count;
                _writer.Write(count);

                foreach (KeyValuePair<string, List<string>> pair in dictionary)
                {
                    string key = pair.Key;
                    Translate(ref key);
                    List<string> value = pair.Value;
                    Translate(ref value);
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
    }
}