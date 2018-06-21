// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A simple string interner designed for IPC.
    /// </summary>
    /// <remarks>
    /// This interner works by providing a way to convert strings to integer IDs.  When used as a form of compression, 
    /// clients will intern their strings and record the set of IDs returned, then transmit those IDs instead of the
    /// original strings.  The interner itself is also transmitted ahead of time, with the IDs, allowing 
    /// reconstruction of the original strings.  This ensures each string is transmitted exactly once.
    /// </remarks>
    internal class LookasideStringInterner : INodePacketTranslatable
    {
        /// <summary>
        /// Index used for null strings.
        /// </summary>
        private const int NullStringIndex = -1;

        /// <summary>
        /// Index used for empty strings.
        /// </summary>
        private const int EmptyStringIndex = -2;

        /// <summary>
        /// The map used to intern strings for serialization.  This map doesn't exist when the strings
        /// are deserialized (it is not needed.)
        /// </summary>
        private readonly Dictionary<string, int> _stringToIdsMap;

        /// <summary>
        /// The list of strings by ID.
        /// </summary>
        private List<string> _strings;

        /// <summary>
        /// Constructor to be used during serialization.
        /// </summary>
        public LookasideStringInterner(StringComparer comparer, int defaultCollectionSize)
        {
            _stringToIdsMap = new Dictionary<string, int>(defaultCollectionSize, comparer);
            _strings = new List<string>(defaultCollectionSize);
        }

        /// <summary>
        /// Constructor to be used during deserialization.
        /// </summary>
        /// <remarks>
        /// Intern cannot be used on this interner if it came from serialization, since we do 
        /// not reconstruct the interning dictionary.
        /// </remarks>
        public LookasideStringInterner(INodePacketTranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Interns the specified string.
        /// </summary>
        /// <param name="str">The string to intern.</param>
        /// <returns>The index representing the string.</returns>
        public int Intern(string str)
        {
            if (str == null)
            {
                return NullStringIndex;
            }
            else if (str.Length == 0)
            {
                return EmptyStringIndex;
            }
            else
            {
                // If stringToIdsMap is null here, it means we probably tried to intern a string to an interner which came from
                // deserialization (and thus doesn't support further interning for efficiency reasons.)  No VerifyThrow here
                // because this function is called a lot.
                if (!_stringToIdsMap.TryGetValue(str, out int index))
                {
                    index = _strings.Count; // This will be the index of the string we are about to add.
                    _stringToIdsMap.Add(str, index);
                    _strings.Add(str);
                }

                return index;
            }
        }

        /// <summary>
        /// Retrieves a string corresponding to the provided index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The corresponding string.</returns>
        public string GetString(int index)
        {
            switch (index)
            {
                case NullStringIndex:
                    return null;

                case EmptyStringIndex:
                    return String.Empty;

                default:
                    return _strings[index];
            }
        }

        /// <summary>
        /// The translator, for serialization.
        /// </summary>
        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _strings);
        }
    }
}
