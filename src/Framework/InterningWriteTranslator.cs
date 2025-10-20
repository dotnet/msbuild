// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Writes strings into a translator with interning / deduplication.
    /// </summary>
    /// <remarks>
    /// This maintains a reusable temporary buffer and lookup table for deduplicating strings within a translatable packet.
    /// All unique strings (as determined by the comparer) will be assigned an incrementing ID and stored into a dictionary.
    /// This ID will be written to a private buffer in place of the string and any repeat occurrences.
    /// When serialized into another translator, the interner will:
    /// 1. Serialize the list of unique strings to an array, where the ID is the index.
    /// 2. Serialize the temporary buffer (aka the packet body) with all interned strings replaced by their ID.
    /// This ordering is important since the reader will need the string lookup table before parsing the body.
    /// As such, two rules need to be followed when using this class:
    /// 1. Any interleaved non-interned writes should be written using the exposed BinaryWriter to keep the overall
    /// packet in sync.
    /// 2. Translate should *only* be called after all internable writes have been processed.
    /// </remarks>
    internal sealed class InterningWriteTranslator : ITranslatable
    {
        private static readonly char[] DirectorySeparatorChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

        private static readonly string IsPathMarker = string.Empty;

        private static readonly string? NotPathMarker = null;

        private readonly ITranslator _translator;

        private readonly MemoryStream _packetStream;

        private List<string> _strings = [];

        private Dictionary<string, int> _stringToIds = [];

        private Dictionary<string, InternPathIds> _stringToPathIds = [];

        internal InterningWriteTranslator()
        {
            _packetStream = new MemoryStream();
            _translator = BinaryTranslator.GetWriteTranslator(_packetStream);

            // Avoid directly exposing the buffered translator - any accidental Intern_xx method calls could go into a
            // recursive loop.
            Writer = _translator.Writer;
        }

        /// <summary>
        /// The writer for the underlying buffer.
        /// Use to forward any non-interning writes into this translator.
        /// </summary>
        internal BinaryWriter Writer { get; }

        /// <summary>
        /// Setup the intern cache and underlying buffer. This allows the interner to be reused.
        /// </summary>
        /// <param name="comparer">The string comparer to use for string deduplication.</param>
        /// <param name="initialCapacity">An estimate of the number of unique strings to be interned.</param>
        internal void Setup(IEqualityComparer<string> comparer, int initialCapacity)
        {
#if NET
            if (_stringToIds.Comparer == comparer)
            {
                // Clear before setting capacity, since dictionaries will rehash every entry.
                _strings.Clear();
                _stringToIds.Clear();
                _stringToPathIds.Clear();
                _strings.EnsureCapacity(initialCapacity);
                _stringToIds.EnsureCapacity(initialCapacity);
                _stringToPathIds.EnsureCapacity(initialCapacity);
            }
            else
            {
#endif
                // If the interner is in a reused translator, the comparer might not match between packets.
                // Just throw away the old collections in this case.
                _strings.Clear();
                _strings.Capacity = initialCapacity;
                _stringToIds = new Dictionary<string, int>(initialCapacity, comparer);
                _stringToPathIds = new Dictionary<string, InternPathIds>(initialCapacity, comparer);
#if NET
            }
#endif
            _packetStream.Position = 0;
            _packetStream.SetLength(0);

            // This is a rough estimate since the final size will depend on the length of each string and the total number
            // of intern cache hits. Assume a mixture of short strings (e.g. item metadata pairs, RAR assembly metadata)
            // and file paths (e.g. item include paths, RAR statefile entries).
            const int CharactersPerString = 32;
            const int BytesPerCharacter = 2;
            const int BytesPerInternedString = 5;
            int internHeaderSize = initialCapacity * CharactersPerString * BytesPerCharacter;
            int packetPayloadSize = initialCapacity * BytesPerInternedString;
            _packetStream.Capacity = internHeaderSize + packetPayloadSize;
        }

        internal void Intern(string str) => _ = InternString(str);

        private int InternString(string str)
        {
            if (!_stringToIds.TryGetValue(str, out int index))
            {
                index = _strings.Count;
                _stringToIds.Add(str, index);
                _strings.Add(str);
            }

            _translator.Translate(ref index);
            return index;
        }

        internal void InternPath(string str)
        {
            // If we've seen a string already and know it's path-like, we just need the index pair.
            if (_stringToPathIds.TryGetValue(str, out InternPathIds pathIds))
            {
                _ = _translator.TranslateNullable(IsPathMarker);
                int directoryId = pathIds.DirectoryId;
                int fileNameId = pathIds.FileNameId;
                _translator.Translate(ref directoryId);
                _translator.Translate(ref fileNameId);
                return;
            }

            // Quick and basic heuristic to check if we have a path-like string.
            int splitId = str.LastIndexOfAny(DirectorySeparatorChars);
            bool hasDirectorySeparator = splitId > -1
                && splitId < str.Length - 1
                && str.IndexOf('%') == -1;

            if (!hasDirectorySeparator)
            {
                // Set a marker to signal the reader to parse this as a single string.
                _ = _translator.TranslateNullable(NotPathMarker);
                _ = InternString(str);
                return;
            }

            string directory = str.Substring(0, splitId + 1);
            string fileName = str.Substring(splitId + 1);

            _ = _translator.TranslateNullable(IsPathMarker);
            int directoryIndex = InternString(directory);
            int fileNameIndex = InternString(fileName);

            _stringToPathIds.Add(str, new InternPathIds(directoryIndex, fileNameIndex));
        }

        public void Translate(ITranslator translator)
        {
            if (translator.Mode != TranslationDirection.WriteToStream)
            {
                throw new InvalidOperationException(
                    $"{nameof(InterningWriteTranslator)} can only be used with {nameof(TranslationDirection.WriteToStream)}.");
            }

            // Write the set of unique strings as the packet header.
            translator.Translate(ref _strings);

            // Write the temporary buffer as the packet body.
            byte[] buffer = _packetStream.GetBuffer();
            int bufferSize = (int)_packetStream.Length;
            translator.Writer.Write(buffer, 0, bufferSize);
        }
    }
}
