// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Reads strings form a translator which contains interned packets.
    /// </summary>
    /// <remarks>
    /// This maintains a reusable lookup table to deserialize packets interned by <see cref="InterningWriteTranslator"/>.
    /// On Translate, the intern header (aka the array of strings indexed by ID) is deserialized.
    /// The caller can then forward reads to deserialize any interned values in the packet body.
    /// </remarks>
    internal sealed class InterningReadTranslator : ITranslatable
    {
        private readonly ITranslator _translator;

        private List<string> _strings = [];

        private Dictionary<InternPathIds, string> _pathIdsToString = [];

        internal InterningReadTranslator(ITranslator translator)
        {
            if (translator.Mode != TranslationDirection.ReadFromStream)
            {
                throw new InvalidOperationException(
                    $"{nameof(InterningReadTranslator)} can only be used with {nameof(TranslationDirection.ReadFromStream)}.");
            }

            _translator = translator;
        }

        internal string? Read()
        {
            int key = -1;
            _translator.Translate(ref key);
            return _strings[key];
        }

        internal string? ReadPath()
        {
            // If the writer set a null marker, read this as a single string.
            if (!_translator.TranslateNullable(string.Empty))
            {
                return Read();
            }

            int directoryKey = -1;
            int fileNameKey = -1;
            _translator.Translate(ref directoryKey);
            _translator.Translate(ref fileNameKey);

            InternPathIds pathIds = new(directoryKey, fileNameKey);

            // Only concatenate paths the first time we encounter a pair.
            if (_pathIdsToString.TryGetValue(pathIds, out string? path))
            {
                return path;
            }

            string directory = _strings[pathIds.DirectoryId];
            string fileName = _strings[pathIds.FileNameId];
            string str = string.Concat(directory, fileName);
            _pathIdsToString.Add(pathIds, str);

            return str;
        }

        public void Translate(ITranslator translator)
        {
            // Only deserialize the intern header since the caller will be reading directly from the stream.
            _translator.Translate(ref _strings);
#if NET
            _pathIdsToString.Clear();
            _pathIdsToString.EnsureCapacity(_strings.Count);
#else
            _pathIdsToString = new(_strings.Count);
#endif
        }
    }
}
