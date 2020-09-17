// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal sealed class ResolveAssemblyReferneceResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ResolveAssemblyReferneceResolver();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                var f = GeneratedResolverGetFormatterHelper.GetFormatter(typeof(T));
                if (f != null)
                {
                    Formatter = (IMessagePackFormatter<T>)f;
                    return;
                }
                else if (typeof(T) == typeof(BuildEventArgs))
                {
                    Formatter = (IMessagePackFormatter<T>)BuildEventArgsFormatter.Instance;
                    return;
                }
            }
        }
    }

    internal static class GeneratedResolverGetFormatterHelper
    {
        private static readonly Dictionary<Type, int> lookup;

        static GeneratedResolverGetFormatterHelper()
        {
            lookup = new Dictionary<Type, int>
            {
                { typeof(ReadOnlyTaskItem[]), 0 },
                { typeof(Dictionary<string, string>), 1 },
                { typeof(List<BuildEventArgs>), 2 },
                { typeof(ReadOnlyTaskItem), 3 },
                { typeof(ResolveAssemblyReferenceRequest), 4 },
                { typeof(ResolveAssemblyReferenceResponse), 5 },
                { typeof(ResolveAssemblyReferenceResult), 6 },
                { typeof(string[]), 7 },
                { typeof(string), 8 },
            };
        }

        internal static object GetFormatter(Type t)
        {
            if (!lookup.TryGetValue(t, out int key))
            {
                return null;
            }

            return key switch
            {
                0 => new ArrayFormatter<ReadOnlyTaskItem>(),
                1 => new DictionaryFormatter<string, string>(),
                2 => new ListFormatter<BuildEventArgs>(),
                3 => new ReadOnlyTaskItemFormatter(),
                4 => new RequestFormatter(),
                5 => new ResponseFormatter(),
                6 => new ResultFormatter(),
                7 => NullableStringArrayFormatter.Instance,
                8 => NullableStringFormatter.Instance,
                _ => null,
            };
        }
    }
}
