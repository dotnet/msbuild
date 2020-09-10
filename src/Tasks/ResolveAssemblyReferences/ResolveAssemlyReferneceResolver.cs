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
    internal sealed class ResolveAssemlyReferneceResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ResolveAssemlyReferneceResolver();

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
                else if (typeof(T) == typeof(BuildMessageEventArgs))
                {
                    Formatter = (IMessagePackFormatter<T>)BuildEventArgsFormatter.Instance;
                    return;
                }
                else if (typeof(T) == typeof(BuildErrorEventArgs))
                {
                    Formatter = (IMessagePackFormatter<T>)BuildEventArgsFormatter.Instance;
                    return;
                }
                else if (typeof(T) == typeof(BuildWarningEventArgs))
                {
                    Formatter = (IMessagePackFormatter<T>)BuildEventArgsFormatter.Instance;
                    return;
                }
                else if (typeof(T) == typeof(CustomBuildEventArgs))
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
                { typeof(List<BuildErrorEventArgs>), 2 },
                { typeof(List<BuildMessageEventArgs>), 3 },
                { typeof(List<BuildWarningEventArgs>), 4 },
                { typeof(List<CustomBuildEventArgs>), 5 },
                { typeof(ReadOnlyTaskItem), 6 },
                { typeof(ResolveAssemblyReferenceRequest), 7 },
                { typeof(ResolveAssemblyReferenceResponse), 8 },
                { typeof(ResolveAssemblyReferenceResult), 9 },
                { typeof(string[]), 10 },
                { typeof(string), 11 },
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
                2 => new ListFormatter<BuildErrorEventArgs>(),
                3 => new ListFormatter<BuildMessageEventArgs>(),
                4 => new ListFormatter<BuildWarningEventArgs>(),
                5 => new ListFormatter<CustomBuildEventArgs>(),
                6 => new ReadOnlyTaskItemFormatter(),
                7 => new ResolveAssemblyReferenceRequestFormatter(),
                8 => new ResolveAssemblyReferenceResponseFormatter(),
                9 => new ResolveAssemblyReferenceResultFormatter(),
                10 => NullableStringArrayFormatter.Instance,
                11 => NullableStringFormatter.Instance,
                _ => null,
            };
        }
    }
}
