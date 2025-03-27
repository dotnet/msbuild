// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.BackEnd
{
    internal sealed class InterningWriteTranslator : ITranslatable
    {
        private List<string> _strings = [];

        private Dictionary<string, int> _stringToIds = [];

        private Dictionary<string, PathIds> _stringToPathIds = [];

        private MemoryStream _packetStream = new();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        // Recursive loop
        internal ITranslator Translator { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        internal void InitCapacity(IEqualityComparer<string> comparer, int count)
        {
            if (Translator == null)
            {
                Translator = BinaryTranslator.GetWriteTranslator(_packetStream, this);
            }

            int capacity = count * 8;
            int bufferCapacity = capacity * 128;
            _stringToIds = new Dictionary<string, int>(count * 8, comparer);
            _stringToPathIds = new Dictionary<string, PathIds>(count * 8, comparer);
            _strings.Clear();
            _strings.Capacity = capacity;
            _packetStream.Position = 0;
            _packetStream.SetLength(0);
            _packetStream.Capacity = bufferCapacity;
        }

        internal void Intern(string str) => InternString(str);

        internal void InternNullable(string str)
        {
            if (!Translator.TranslateNullable(str))
            {
                return;
            }

            InternString(str);
        }

        private int InternString(string str)
        {
            if (!_stringToIds.TryGetValue(str, out int index))
            {
                index = _strings.Count;
                _stringToIds.Add(str, index);
                _strings.Add(str);
            }

            Translator.Translate(ref index);
            return index;
        }

        internal void InternNullablePath(string str)
        {
            if (!Translator.TranslateNullable(str))
            {
                return;
            }

            InternPath(str);
        }

        internal void InternPath(string str)
        {
            if (_stringToPathIds.TryGetValue(str, out PathIds pathIds))
            {
                _ = Translator.TranslateNullable(string.Empty);
                int directoryId = pathIds.DirectoryId;
                int fileNameId = pathIds.FileNameId;
                Translator.Translate(ref directoryId);
                Translator.Translate(ref fileNameId);
                return;
            }

            int splitId = str.LastIndexOf(Path.DirectorySeparatorChar);

            if (splitId == -1)
            {
                splitId = str.LastIndexOf(Path.AltDirectorySeparatorChar);
            }

            bool hasDirectorySeparator = splitId > -1
                && splitId < str.Length - 1
                && str.IndexOf('%') == -1;

            if (!hasDirectorySeparator)
            {
                string? dummy = null;
                _ = Translator.TranslateNullable(dummy);
                _ = InternString(str);
                return;
            }

            // If we've seen a string already and know it's pathlike, we just need the index duo
            string directory = str.Substring(0, splitId + 1);
            string fileName = str.Substring(splitId + 1);

            _ = Translator.TranslateNullable(string.Empty);
            int directoryIndex = InternString(directory);
            int fileNameIndex = InternString(fileName);

            _stringToPathIds.Add(str, new PathIds(directoryIndex, fileNameIndex));
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _strings);
            byte[] buffer = _packetStream.GetBuffer();
            int bufferSize = (int)_packetStream.Length;
            translator.Writer.Write(buffer, 0, bufferSize);
        }

        private readonly record struct PathIds(int DirectoryId, int FileNameId);
    }
}
