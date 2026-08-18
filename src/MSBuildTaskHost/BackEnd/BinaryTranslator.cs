// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Build.TaskHost.Exceptions;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// This class is responsible for serializing and deserializing simple types to and
/// from the byte streams used to communicate INodePacket-implementing classes.
/// Each class implements a Translate method on INodePacket which takes this class
/// as a parameter, and uses it to store and retrieve fields to the stream.
/// </summary>
internal static class BinaryTranslator
{
    private static byte[] EmptyByteArray => field ??= [];

    /// <summary>
    /// Returns a read-only serializer.
    /// </summary>
    /// <returns>The serializer.</returns>
    internal static ITranslator GetReadTranslator(Stream stream, BinaryReaderFactory buffer)
        => new BinaryReadTranslator(stream, buffer);

    /// <summary>
    /// Returns a write-only serializer.
    /// </summary>
    /// <param name="stream">The stream containing data to serialize.</param>
    /// <returns>The serializer.</returns>
    internal static ITranslator GetWriteTranslator(Stream stream)
        => new BinaryWriteTranslator(stream);

    /// <summary>
    /// Implementation of ITranslator for reading from a stream.
    /// </summary>
    private class BinaryReadTranslator(Stream packetStream, BinaryReaderFactory buffer) : ITranslator
    {
        /// <summary>
        /// Gets the reader, if any.
        /// </summary>
        public BinaryReader Reader { get; } = buffer.Create(packetStream);

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
        /// Gets the current serialization mode.
        /// </summary>
        public TranslationDirection Mode => TranslationDirection.ReadFromStream;

        /// <inheritdoc/>
        public byte NegotiatedPacketVersion { get; set; }

        /// <summary>
        /// Delegates the Dispose call the to the underlying BinaryReader.
        /// </summary>
        public void Dispose()
            => Reader.Close();

        /// <summary>
        /// Translates a boolean.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref bool value)
            => value = Reader.ReadBoolean();

        /// <summary>
        /// Translates an <see langword="bool"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref bool[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = Reader.ReadInt32();
            array = new bool[count];

            for (int i = 0; i < count; i++)
            {
                array[i] = Reader.ReadBoolean();
            }
        }

        /// <summary>
        /// Translates a byte.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref byte value)
            => value = Reader.ReadByte();

        /// <summary>
        /// Translates a short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref short value)
            => value = Reader.ReadInt16();

        /// <summary>
        /// Translates an unsigned short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref ushort value)
            => value = Reader.ReadUInt16();

        /// <summary>
        /// Translates an integer.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref int value)
            => value = Reader.ReadInt32();

        /// <summary>
        /// Translates an <see langword="int"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref int[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = Reader.ReadInt32();
            array = new int[count];

            for (int i = 0; i < count; i++)
            {
                array[i] = Reader.ReadInt32();
            }
        }

        /// <summary>
        /// Translates a long.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref long value)
            => value = Reader.ReadInt64();

        /// <summary>
        /// Translates a double.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref double value)
            => value = Reader.ReadDouble();

        /// <summary>
        /// Translates a string.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref string? value)
        {
            if (!TranslateNullable(value))
            {
                return;
            }

            value = Reader.ReadString();
        }

        /// <summary>
        /// Translates a byte array.
        /// </summary>
        /// <param name="byteArray">The array to be translated.</param>
        public void Translate(ref byte[]? byteArray)
        {
            if (!TranslateNullable(byteArray))
            {
                return;
            }

            int count = Reader.ReadInt32();
            byteArray = count > 0
                ? Reader.ReadBytes(count)
                : EmptyByteArray;
        }

        /// <summary>
        /// Translates a string array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref string[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = Reader.ReadInt32();
            array = new string[count];

            for (int i = 0; i < count; i++)
            {
                array[i] = Reader.ReadString();
            }
        }

        /// <summary>
        /// Translates a collection of T into the specified type using an <see cref="ObjectTranslator{T}"/> and <see cref="NodePacketCollectionCreator{L}"/>.
        /// </summary>
        /// <param name="collection">The collection to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the values in the collection.</param>
        /// <param name="collectionFactory">The factory to create the ICollection.</param>
        /// <typeparam name="T">The type contained in the collection.</typeparam>
        /// <typeparam name="L">The type of collection to be created.</typeparam>
        public void Translate<T, L>(ref ICollection<T>? collection, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory)
            where L : ICollection<T>
        {
            if (!TranslateNullable(collection))
            {
                return;
            }

            int count = Reader.ReadInt32();
            collection = collectionFactory(count);

            for (int i = 0; i < count; i++)
            {
                T value = default!;
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
            TranslateEnum(ref kind, 0);
            value = new DateTime(Reader.ReadInt64(), kind);
        }

        /// <summary>
        /// Translates a CultureInfo.
        /// </summary>
        /// <param name="value">The CultureInfo to translate.</param>
        public void TranslateCulture(ref CultureInfo? value)
        {
            string cultureName = Reader.ReadString();

            // It may be that some culture codes are accepted on later .net framework versions
            // but not on the older 3.5 or 2.0. Fallbacks are required in this case to prevent
            // exceptions
            try
            {
                value = new CultureInfo(cultureName);
            }
            catch
            {
                value = CultureInfo.CurrentCulture;
            }
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
            numericValue = Reader.ReadInt32();
            Type enumType = value.GetType();
            value = (T)Enum.ToObject(enumType, numericValue);
        }

        public void TranslateException(ref Exception? value)
        {
            if (!TranslateNullable(value))
            {
                return;
            }

            value = BuildExceptionBase.ReadExceptionFromTranslator(this);
        }

        /// <summary>
        /// Translates a dictionary of { string, string }.
        /// </summary>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        public void TranslateDictionary(ref Dictionary<string, string?>? dictionary, IEqualityComparer<string> comparer)
        {
            if (!TranslateNullable(dictionary))
            {
                return;
            }

            int count = Reader.ReadInt32();
            dictionary = new Dictionary<string, string?>(count, comparer);

            for (int i = 0; i < count; i++)
            {
                string? key = null;
                Translate(ref key);
                string? value = null;
                Translate(ref value);

                // NOTE: This can throw if key is null.
                dictionary[key!] = value;
            }
        }

        /// <inheritdoc/>
        public void TranslateDictionary<T>(
            ref Dictionary<string, T>? dictionary,
            IEqualityComparer<string> comparer,
            ObjectTranslatorWithValueFactory<T> objectTranslator,
            NodePacketValueFactory<T> valueFactory)
            where T : class
        {
            if (!TranslateNullable(dictionary))
            {
                return;
            }

            int count = Reader.ReadInt32();
            dictionary = new Dictionary<string, T>(count, comparer);

            for (int i = 0; i < count; i++)
            {
                string? key = null;
                Translate(ref key);
                T value = default!;
                objectTranslator(this, valueFactory, ref value);

                // NOTE: This can throw if key is null.
                dictionary[key!] = value;
            }
        }

        /// <summary>
        /// Reads in the boolean which says if this object is null or not.
        /// </summary>
        /// <typeparam name="T">The type of object to test.</typeparam>
        /// <returns>True if the object should be read, false otherwise.</returns>
        public bool TranslateNullable<T>(T? value)
            where T : class
        {
            bool haveRef = Reader.ReadBoolean();
            return haveRef;
        }
    }

    /// <summary>
    /// Implementation of ITranslator for writing to a stream.
    /// </summary>
    /// <param name="packetStream">The stream serving as the source or destination of data.</param>
    private class BinaryWriteTranslator(Stream packetStream) : ITranslator
    {
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
        public BinaryWriter Writer { get; } = new BinaryWriter(packetStream);

        /// <summary>
        /// Gets the current serialization mode.
        /// </summary>
        public TranslationDirection Mode => TranslationDirection.WriteToStream;

        /// <inheritdoc/>
        public byte NegotiatedPacketVersion { get; set; }

        /// <summary>
        /// Delegates the Dispose call the to the underlying BinaryWriter.
        /// </summary>
        public void Dispose()
            => Writer.Close();

        /// <summary>
        /// Translates a boolean.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref bool value)
            => Writer.Write(value);

        /// <summary>
        /// Translates an <see langword="bool"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref bool[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = array.Length;
            Writer.Write(count);

            for (int i = 0; i < count; i++)
            {
                Writer.Write(array[i]);
            }
        }

        /// <summary>
        /// Translates a byte.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref byte value)
            => Writer.Write(value);

        /// <summary>
        /// Translates a short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref short value)
            => Writer.Write(value);

        /// <summary>
        /// Translates an unsigned short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref ushort value)
            => Writer.Write(value);

        /// <summary>
        /// Translates an integer.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref int value)
            => Writer.Write(value);

        /// <summary>
        /// Translates an <see langword="int"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref int[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = array.Length;
            Writer.Write(count);

            for (int i = 0; i < count; i++)
            {
                Writer.Write(array[i]);
            }
        }

        /// <summary>
        /// Translates a long.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref long value)
            => Writer.Write(value);

        /// <summary>
        /// Translates a double.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref double value)
            => Writer.Write(value);

        /// <summary>
        /// Translates a string.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        public void Translate(ref string? value)
        {
            if (!TranslateNullable(value))
            {
                return;
            }

            Writer.Write(value);
        }

        /// <summary>
        /// Translates a string array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        public void Translate(ref string[]? array)
        {
            if (!TranslateNullable(array))
            {
                return;
            }

            int count = array.Length;
            Writer.Write(count);

            for (int i = 0; i < count; i++)
            {
                Writer.Write(array[i]);
            }
        }

        /// <summary>
        /// Translates a collection of T into the specified type using an <see cref="ObjectTranslator{T}"/> and <see cref="NodePacketCollectionCreator{L}"/>.
        /// </summary>
        /// <param name="collection">The collection to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the values in the collection.</param>
        /// <param name="collectionFactory">The factory to create the ICollection.</param>
        /// <typeparam name="T">The type contained in the collection.</typeparam>
        /// <typeparam name="L">The type of collection to be created.</typeparam>
        public void Translate<T, L>(
            ref ICollection<T>? collection,
            ObjectTranslator<T> objectTranslator,
            NodePacketCollectionCreator<L> collectionFactory)
            where L : ICollection<T>
        {
            if (!TranslateNullable(collection))
            {
                return;
            }

            Writer.Write(collection.Count);

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
            TranslateEnum(ref kind, (int)kind);
            Writer.Write(value.Ticks);
        }

        /// <summary>
        /// Translates a CultureInfo.
        /// </summary>
        /// <param name="value">The CultureInfo.</param>
        public void TranslateCulture(ref CultureInfo? value)
            => Writer.Write(value!.Name);

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
            => Writer.Write(numericValue);

        public void TranslateException(ref Exception? value)
        {
            if (!TranslateNullable(value))
            {
                return;
            }

            BuildExceptionBase.WriteExceptionToTranslator(this, value);
        }

        /// <summary>
        /// Translates a byte array.
        /// </summary>
        /// <param name="byteArray">The byte array to be translated.</param>
        public void Translate(ref byte[]? byteArray)
        {
            if (!TranslateNullable(byteArray))
            {
                return;
            }

            int length = byteArray.Length;

            Writer.Write(length);
            if (length > 0)
            {
                Writer.Write(byteArray, 0, length);
            }
        }

        /// <summary>
        /// Translates a dictionary of { string, string }.
        /// </summary>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        public void TranslateDictionary(ref Dictionary<string, string?>? dictionary, IEqualityComparer<string> comparer)
        {
            if (!TranslateNullable(dictionary))
            {
                return;
            }

            int count = dictionary.Count;
            Writer.Write(count);

            foreach (KeyValuePair<string, string?> pair in dictionary)
            {
                string? key = pair.Key;
                Translate(ref key);
                string? value = pair.Value;
                Translate(ref value);
            }
        }

        /// <inheritdoc/>
        public void TranslateDictionary<T>(
            ref Dictionary<string, T>? dictionary,
            IEqualityComparer<string> comparer,
            ObjectTranslatorWithValueFactory<T> objectTranslator,
            NodePacketValueFactory<T> valueFactory)
            where T : class
        {
            if (!TranslateNullable(dictionary))
            {
                return;
            }

            int count = dictionary.Count;
            Writer.Write(count);

            foreach (KeyValuePair<string, T> pair in dictionary)
            {
                string? key = pair.Key;
                Translate(ref key);
                T value = pair.Value;
                objectTranslator(this, valueFactory, ref value);
            }
        }

        /// <summary>
        /// Writes out the boolean which says if this object is null or not.
        /// </summary>
        /// <param name="value">The object to test.</param>
        /// <typeparam name="T">The type of object to test.</typeparam>
        /// <returns>True if the object should be written, false otherwise.</returns>
        public bool TranslateNullable<T>([NotNullWhen(true)] T? value)
            where T : class
        {
            bool haveRef = value != null;
            Writer.Write(haveRef);
            return haveRef;
        }
    }
}
