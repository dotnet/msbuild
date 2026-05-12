// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

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
        /// Gets or sets the packet version associated with the stream.
        /// This can be used to exclude various fields from translation for backwards compatibility,
        /// e.g. when Writer introduces information that should be skipped in the Reader stream.
        /// </summary>
        byte PacketVersion { get; set; }

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
        /// Translates an unsigned integer.
        /// </summary>
        /// <param name="unsignedInteger">The unsigned integer to translate.</param>
        void Translate(ref uint unsignedInteger);

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
        /// Translates a list of strings
        /// </summary>
        /// <param name="list">The list to be translated.</param>
        void Translate(ref List<string> list);

        /// <summary>
        /// Translates a set of strings
        /// </summary>
        /// <param name="set">The set to be translated.</param>
        void Translate(ref HashSet<string> set);

        /// <summary>
        /// Translates a list of T using an <see cref="ObjectTranslator{T}"/>
        /// </summary>
        /// <param name="list">The list to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the items in the list</param>
        /// <typeparam name="T">A TaskItemType</typeparam>
        void Translate<T>(ref List<T> list, ObjectTranslator<T> objectTranslator);

        /// <summary>
        /// Translates a list of T using an <see cref="ObjectTranslator{T}"/>
        /// </summary>
        /// <param name="list">The list to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the items in the list</param>
        /// <param name="valueFactory">The factory to use to create the value.</param>
        /// <typeparam name="T">A TaskItemType</typeparam>
        void Translate<T>(ref List<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory);

        /// <summary>
        /// Translates a list of T using an <see cref="ObjectTranslator{T}"/> anda collection factory
        /// </summary>
        /// <param name="list">The list to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the items in the list</param>
        /// <typeparam name="T">An ITranslatable subtype</typeparam>
        /// <typeparam name="L">An IList subtype</typeparam>
        /// <param name="collectionFactory">factory to create a collection</param>
        void Translate<T, L>(ref IList<T> list, ObjectTranslator<T> objectTranslator, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>;

        /// <summary>
        /// Translates a list of T using an <see cref="ObjectTranslator{T}"/> and a collection factory
        /// </summary>
        /// <param name="list">The list to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the items in the list</param>
        /// <param name="valueFactory">The factory to use to create the value.</param>
        /// <typeparam name="T">An ITranslatable subtype</typeparam>
        /// <typeparam name="L">An IList subtype</typeparam>
        /// <param name="collectionFactory">factory to create a collection</param>
        void Translate<T, L>(ref IList<T> list, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<L> collectionFactory) where L : IList<T>;

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
        /// Translates a TimeSpan.
        /// </summary>
        /// <param name="value">The value to be translated.</param>
        void Translate(ref TimeSpan value);

        // MSBuildTaskHost is based on CLR 3.5, which does not have the 6-parameter constructor for BuildEventContext,
        // which is what current implementations of this method use.  However, it also does not ever need to translate
        // BuildEventContexts, so it should be perfectly safe to compile this method out of that assembly. I am compiling
        // the method out of the interface as well, instead of just making the method empty, so that if we ever do need
        // to translate BuildEventContexts from the CLR 3.5 task host, it will become immediately obvious, rather than
        // failing or misbehaving silently.
#if !CLR2COMPATIBILITY

        /// <summary>
        /// Translates a BuildEventContext
        /// </summary>
        /// <remarks>
        /// This method exists only because there is no serialization method built into the BuildEventContext
        /// class, and it lives in Framework and we don't want to add a public method to it.
        /// </remarks>
        /// <param name="value">The context to be translated.</param>
        void Translate(ref BuildEventContext value);
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
        void TranslateEnum<T>(ref T value, int numericValue)
            where T : struct, Enum;

        void TranslateException(ref Exception value);

        /// <summary>
        /// Translates an object implementing INodePacketTranslatable.
        /// </summary>
        /// <typeparam name="T">The reference type.</typeparam>
        /// <param name="value">The value to be translated.</param>
        void Translate<T>(ref T value)
            where T : ITranslatable, new();

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
        /// Translates a byte array
        /// </summary>
        /// <param name="byteArray">The array to be translated.</param>
        /// <param name="length">The length of array which will be used in translation</param>
        void Translate(ref byte[] byteArray, ref int length);

        /// <summary>
        /// Translates an array of objects implementing INodePacketTranslatable.
        /// </summary>
        /// <typeparam name="T">The reference type.</typeparam>
        /// <param name="array">The array to be translated.</param>
        void TranslateArray<T>(ref T[] array)
            where T : ITranslatable, new();

        /// <summary>
        /// Translates an array of objects using an <see cref="ObjectTranslator{T}"/>.
        /// </summary>
        /// <typeparam name="T">The reference type.</typeparam>
        /// <param name="array">The array to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the elements in the array.</param>
        /// <param name="valueFactory">The factory to use to create the value.</param>
        void TranslateArray<T>(ref T[] array, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory);

        /// <summary>
        /// Translates a dictionary of { string, string }.
        /// </summary>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        void TranslateDictionary(ref Dictionary<string, string> dictionary, IEqualityComparer<string> comparer);

        /// <summary>
        /// Translates a TaskHostParameters.
        /// </summary>
        /// <param name="value">The TaskHostParameters to translate.</param>
        void Translate(ref TaskHostParameters value);

        /// <summary>
        /// Translates a dictionary of { string, string } adding additional entries.
        /// </summary>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="comparer">The comparer used to instantiate the dictionary.</param>
        /// <param name="additionalEntries">Additional entries to be translated</param>
        /// <param name="additionalEntriesKeys">Additional entries keys</param>
        /// <remarks>
        /// This overload is needed for a workaround concerning serializing BuildResult with a version.
        /// It serializes/deserializes additional entries together with the main dictionary.
        /// </remarks>
        void TranslateDictionary(ref IDictionary<string, string> dictionary, IEqualityComparer<string> comparer, ref Dictionary<string, string> additionalEntries, HashSet<string> additionalEntriesKeys);

        void TranslateDictionary(ref IDictionary<string, string> dictionary, NodePacketCollectionCreator<IDictionary<string, string>> collectionCreator);

        void TranslateDictionary(ref Dictionary<string, DateTime> dictionary, StringComparer comparer);

        void TranslateDictionary<K, V>(ref IDictionary<K, V> dictionary, ObjectTranslator<K> keyTranslator, ObjectTranslator<V> valueTranslator, NodePacketCollectionCreator<IDictionary<K, V>> dictionaryCreator);

        void TranslateDictionary<K, V>(ref IDictionary<K, V> dictionary, ObjectTranslator<K> keyTranslator, ObjectTranslatorWithValueFactory<V> valueTranslator, NodePacketValueFactory<V> valueFactory, NodePacketCollectionCreator<IDictionary<K, V>> dictionaryCreator);

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
        /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
        /// </summary>
        /// <typeparam name="D">The reference type for the dictionary.</typeparam>
        /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the values in the dictionary.</param>
        /// <param name="valueFactory">The factory to use to create the value.</param>
        void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory)
            where D : IDictionary<string, T>, new()
            where T : class;

        /// <summary>
        /// Translates a dictionary of { string, T } for dictionaries with public parameterless constructors.
        /// </summary>
        /// <typeparam name="D">The reference type for the dictionary.</typeparam>
        /// <typeparam name="T">The reference type for values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to be translated.</param>
        /// <param name="objectTranslator">The translator to use for the values in the dictionary</param>
        /// /// <param name="valueFactory">The factory to use to create the value.</param>
        /// <param name="collectionCreator">A factory used to create the dictionary.</param>
        void TranslateDictionary<D, T>(ref D dictionary, ObjectTranslatorWithValueFactory<T> objectTranslator, NodePacketValueFactory<T> valueFactory, NodePacketCollectionCreator<D> collectionCreator)
            where D : IDictionary<string, T>
            where T : class;

        /// <summary>
        /// Translates the boolean that says whether this value is null or not
        /// </summary>
        /// <param name="value">The object to test.</param>
        /// <typeparam name="T">The type of object to test.</typeparam>
        /// <returns>True if the object should be written, false otherwise.</returns>
        bool TranslateNullable<T>(T value);

        /// <summary>
        /// Creates a scope which activates string interning / deduplication for any Intern_xx method.
        /// This should generally be called from the root level packet.
        /// </summary>
        /// <param name="comparer">The string comparer to use when populating the intern cache.</param>
        /// <param name="initialCapacity">The initial capacity of the intern cache.</param>
        /// <param name="internBlock">A delegate providing a translator, in which all Intern_xx calls will go through the intern cache.</param>
        /// <remarks>
        /// Packet interning is implemented via a header with an array of all interned strings, followed by the body in
        /// which any interned / duplicated strings are replaced by their ID.
        /// <see cref="TranslationDirection"/> modes have different ordering requirements, so it would not be
        /// possible to implement direction-agnostic serialization via the Intern_xx methods alone:
        /// - Write: Because we don't know the full list of strings ahead of time, we need to create a temporary buffer
        ///   for the packet body, which we can later offset when flushing to the real stream.
        /// - Read: The intern header needs to be deserialized before the packet body, otherwise we won't know what
        ///   string each ID maps to.
        /// This method abstracts these requirements to the caller, such that the underlying translator will
        /// automatically handle the appropriate IO ordering when entering / exiting the delegate scope.
        /// </remarks>
        void WithInterning(IEqualityComparer<string> comparer, int initialCapacity, Action<ITranslator> internBlock);

        /// <summary>
        /// Interns the string if the translator is currently within an intern block.
        /// Otherwise, this forwards to the regular Translate method.
        /// </summary>
        /// <param name="str">The value to be translated.</param>
        /// <param name="nullable">
        /// Whether to null check and translate the nullable marker.
        /// Setting this to false can reduce packet sizes when interning large numbers of strings
        /// which are validated to always be non-null, such as dictionary keys.
        /// </param>
        void Intern(ref string str, bool nullable = true);

        /// <summary>
        /// Interns each string in the array if the translator is currently within an intern block.
        /// Otherwise, this forwards to the regular Translate method. To match behavior, all strings
        /// assumed to be non-null.
        /// </summary>
        /// <param name="array">The array to be translated.</param>
        void Intern(ref string[] array);

        /// <summary>
        /// Interns the string if the translator is currently within an intern block.
        /// Otherwise, this forwards to the regular Translate method.
        /// If the string is determined to be path-like, the path components will be interned separately.
        /// </summary>
        /// <param name="str">The value to be translated.</param>
        /// <param name="nullable">
        /// Whether to null check and translate the nullable marker.
        /// Setting this to false can reduce packet sizes when interning large numbers of strings
        /// which are validated to always be non-null, such as dictionary keys.
        /// </param>
        void InternPath(ref string str, bool nullable = true);
    }
}
