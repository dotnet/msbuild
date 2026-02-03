// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This delegate is used for objects which do not have public parameterless constructors and must be constructed using
    /// another method.  When invoked, this delegate should return a new object which has been translated appropriately.
    /// </summary>
    /// <typeparam name="T">The type to be translated.</typeparam>
    internal delegate T NodePacketValueFactory<T>(ITranslator translator);

    /// <summary>
    /// Delegate for users that want to translate an arbitrary structure that doesn't implement <see cref="ITranslatable"/> (e.g. translating a complex collection)
    /// </summary>
    /// <param name="translator">the translator</param>
    /// <param name="objectToTranslate">the object to translate</param>
    internal delegate void ObjectTranslator<T>(ITranslator translator, ref T objectToTranslate);

    /// <summary>
    /// Delegate for users that want to translate an arbitrary structure that doesn't implement <see cref="ITranslatable"/> (e.g. translating a complex collection)
    /// </summary>
    /// <param name="translator">the translator</param>
    /// <param name="valueFactory">The factory to use to create the value.</param>
    /// <param name="objectToTranslate">the object to translate</param>
    internal delegate void ObjectTranslatorWithValueFactory<T>(ITranslator translator, NodePacketValueFactory<T> valueFactory, ref T objectToTranslate);

    /// <summary>
    /// This delegate is used to create arbitrary collection types for serialization.
    /// </summary>
    /// <typeparam name="T">The type of dictionary to be created.</typeparam>
    internal delegate T NodePacketCollectionCreator<T>(int capacity);

    /// <summary>
    /// The serialization mode.
    /// </summary>
    internal enum TranslationDirection
    {
        /// <summary>
        /// Indicates the serializer is operating in write mode.
        /// </summary>
        WriteToStream,

        /// <summary>
        /// Indicates the serializer is operating in read mode.
        /// </summary>
        ReadFromStream
    }

    /// <summary>
    /// This interface represents an object which aids objects in serializing and
    /// deserializing INodePackets.
    /// </summary>
    /// <remarks>
    /// The reason we bother with a custom serialization mechanism at all is two fold:
    /// 1. The .Net serialization mechanism is inefficient, even if you implement ISerializable
    ///    with your own custom mechanism.  This is because the serializer uses a bag called
    ///    SerializationInfo into which you are expected to drop all your data.  This adds
    ///    an unnecessary level of indirection to the serialization routines and prevents direct,
    ///    efficient access to the byte-stream.
    /// 2. You have to implement both a reader and writer part, which introduces the potential for
    ///    error should the classes be later modified.  If the reader and writer methods are not
    ///    kept in perfect sync, serialization errors will occur.  Our custom serializer eliminates
    ///    that by ensuring a single Translate method on a given object can handle both reads and
    ///    writes without referencing any field more than once.
    /// </remarks>
    internal interface ITranslator : IDisposable
    {
        /// <summary>
        /// Gets or sets the negotiated packet version between the communicating nodes.
        /// This represents the minimum packet version supported by both the sender and receiver,
        /// ensuring backward compatibility during cross-version communication.
        /// </summary>
        /// <remarks>
        /// This version is determined during the initial handshake between nodes and may differ
        /// from NodePacketTypeExtensions.PacketVersion when nodes are running different MSBuild versions.
        /// The negotiated version is used to conditionally serialize/deserialize fields that may
        /// not be supported by older packet versions.
        /// </remarks>
        byte NegotiatedPacketVersion { get; set; }

        /// <summary>
        /// Returns the current serialization mode.
        /// </summary>
        TranslationDirection Mode
        {
            get;
        }

        /// <summary>
        /// Returns the binary reader.
        /// </summary>
        /// <remarks>
        /// This should ONLY be used when absolutely necessary for translation.  It is generally unnecessary for the
        /// translating object to know the direction of translation.  Use one of the Translate methods instead.
        /// </remarks>
        BinaryReader Reader
        {
            get;
        }

        /// <summary>
        /// Returns the binary writer.
        /// </summary>
        /// <remarks>
        /// This should ONLY be used when absolutely necessary for translation.  It is generally unnecessary for the
        /// translating object to know the direction of translation.  Use one of the Translate methods instead.
        /// </remarks>
        BinaryWriter Writer
        {
            get;
        }

        /// <summary>
        /// Translates a boolean.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref bool value);

        /// <summary>
        /// Translates an <see langword="bool"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        void Translate(ref bool[] array);

        /// <summary>
        /// Translates a byte.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref byte value);

        /// <summary>
        /// Translates a short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref short value);

        /// <summary>
        /// Translates a unsigned short.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref ushort value);

        /// <summary>
        /// Translates an integer.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref int value);

        /// <summary>
        /// Translates an <see langword="int"/> array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        void Translate(ref int[] array);

        /// <summary>
        /// Translates a long.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref long value);

        /// <summary>
        /// Translates a string.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref string value);

        /// <summary>
        /// Translates a double.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref double value);

        /// <summary>
        /// Translates a string array.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        void Translate(ref string[] array);

        /// <summary>
        /// Translates a collection of T into the specified type using an <see cref="ObjectTranslator{T}"/> and <see cref="NodePacketCollectionCreator{L}"/>
        /// </summary>
        /// <param name="collection">The collection to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the values in the collection.</param>
        /// <param name="collectionFactory">The factory to create the ICollection.</param>
        /// <typeparam name="T">The type contained in the collection.</typeparam>
        /// <typeparam name="L">The type of collection to be created.</typeparam>
        void Translate<T, L>(ref ICollection<T> collection, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : ICollection<T>;

        /// <summary>
        /// Translates a DateTime.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref DateTime value);

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
        void TranslateEnum<T>(ref T value, int numericValue)
            where T : struct, Enum;

        void TranslateException(ref Exception value);

        /// <summary>
        /// Translates a culture
        /// </summary>
        /// <param name="culture">The culture</param>
        void TranslateCulture(ref CultureInfo culture);

        /// <summary>
        /// Translates a byte array
        /// </summary>
        /// <param name="byteArray">The array to be translated.</param>
        void Translate(ref byte[] byteArray);

        /// <summary>
        /// Translates a dictionary of { string, string }.
        /// </summary>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer);

        /// <summary>
        /// Translates a dictionary of { string, T }.
        /// </summary>
        /// <typeparam name="T">The reference type for the values, which implements INodePacketTranslatable.</typeparam>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        /// <param name="objectTranslator">The translator to use for the values in the dictionary</param>
        /// /// <param name="valueFactory">The factory to use to create the value.</param>
        void TranslateDictionary<T>(ref Dictionary<string, T> dictionary, IEqualityComparer<string> comparer, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            where T : class;

        /// <summary>
        /// Translates the boolean that says whether this value is null or not
        /// </summary>
        /// <param name="value">The object to test.</param>
        /// <typeparam name="T">The type of object to test.</typeparam>
        /// <returns>True if the object should be written, false otherwise.</returns>
        bool TranslateNullable<T>(T value);
    }
}
