// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Execution
{
    internal class GlobalPropertiesLookup : IReadOnlyDictionary<string, string?>
    {
        internal static IReadOnlyDictionary<string, string?> ToGlobalPropertiesLookup(
            PropertyDictionary<ProjectPropertyInstance>? backing)
        {
            if (backing == null)
            {
                return ImmutableDictionary<string, string?>.Empty;
            }

            return new GlobalPropertiesLookup(backing);
        }

        private GlobalPropertiesLookup(IDictionary<string, ProjectPropertyInstance> backingProperties)
        {
            _backingProperties = backingProperties;
        }

        private readonly IDictionary<string, ProjectPropertyInstance> _backingProperties;

        public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
            => _backingProperties
                .Select(p => new KeyValuePair<string, string?>(p.Key, ExtractEscapedValue(p.Value)))
                .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _backingProperties.Count;
        public bool ContainsKey(string key) => _backingProperties.ContainsKey(key);

        public bool TryGetValue(string key, out string? value)
        {
            if (_backingProperties.TryGetValue(key, out var property))
            {
                value = ExtractEscapedValue(property);
                return true;
            }

            value = null;
            return false;
        }

        public string? this[string key] => ExtractEscapedValue(_backingProperties[key]);

        public IEnumerable<string> Keys => _backingProperties.Keys;
        public IEnumerable<string?> Values => _backingProperties.Values.Select(ExtractEscapedValue);

        private static string? ExtractEscapedValue(ProjectPropertyInstance property) => ((IValued)property).EscapedValue;
    }
}
