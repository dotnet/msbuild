// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class RarMetadataInternCache : ITranslatable
    {
        private Dictionary<string, int> _stringToId = new(StringComparer.Ordinal);

        private List<string> _idToString = [];

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _idToString);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                if (_stringToId.Count != _idToString.Count)
                {
                    _stringToId = new Dictionary<string, int>(_idToString.Count, StringComparer.Ordinal);

                    for (int i = 0; i < _idToString.Count; i++)
                    {
                        string str = _idToString[i];
                        _stringToId[str] = i;
                    }
                }
            }
        }

        internal string GetString(int id)
        {
            return _idToString[id];
        }

        internal int Intern(string str)
        {
            if (_stringToId.TryGetValue(str, out int id))
            {
                return id;
            }

            id = _idToString.Count;
            _idToString.Add(str);
            _stringToId[str] = id;

            return id;
        }
    }
}