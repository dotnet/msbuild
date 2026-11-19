// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Expansion;

internal static class PropertyValueConverter
{
    private const int InitialCapacity = 256;

    public static string ToString(object? value)
        => value switch
        {
            null => string.Empty,
            string s => s,
            _ => ConvertNonString(value),
        };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ConvertNonString(object value) => value switch
    {
        IDictionary d => d.Count > 0
            ? ConvertDictionary(d)
            : string.Empty,

        Array a => a.Length > 0
            ? ConvertArray(a)
            : string.Empty,

        IEnumerable e => ConvertEnumerable(e),

        // The fall back is always to just convert to a string directly.
        // Issue: https://github.com/dotnet/msbuild/issues/9757
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static string ConvertDictionary(IDictionary dictionary)
    {
        using Writer writer = new(InitialCapacity);
        writer.AppendDictionary(dictionary);

        return writer.ToString();
    }

    private static string ConvertArray(Array array)
    {
        using Writer writer = new(InitialCapacity);
        writer.AppendArray(array);

        return writer.ToString();
    }

    private static string ConvertEnumerable(IEnumerable enumerable)
    {
        using Writer writer = new(InitialCapacity);
        writer.AppendEnumerable(enumerable);

        return writer.ToString();
    }

    private ref struct Writer(int initialCapacity)
    {
        private ValueStringBuilder _builder = new(initialCapacity);
        private int _escapeDepth;

        public void Dispose()
            => _builder.Dispose();

        public override readonly string ToString()
            => Strings.WeakIntern(_builder.AsSpan());

        public void AppendDictionary(IDictionary dictionary)
        {
            int startLength = _builder.Length;

            // If the return type is an IDictionary, then we convert this to
            // a semi-colon delimited set of A=B pairs.
            // Key and Value are converted to string and escaped
            foreach (DictionaryEntry entry in dictionary)
            {
                if (_builder.Length > startLength)
                {
                    AppendSeparator();
                }

                // convert and escape each key and value in the dictionary entry
                AppendElement(entry.Key);
                _builder.Append("=");
                AppendElement(entry.Value);
            }
        }

        public void AppendArray(Array array)
        {
            int startLength = _builder.Length;

            if (array.Rank == 1)
            {
                int lowerBound = array.GetLowerBound(0);
                for (int i = 0; i < array.Length; i++)
                {
                    if (_builder.Length > startLength)
                    {
                        AppendSeparator();
                    }

                    AppendElement(array.GetValue(lowerBound + i));
                }

                return;
            }

            foreach (object element in array)
            {
                if (_builder.Length > startLength)
                {
                    AppendSeparator();
                }

                AppendElement(element);
            }
        }

        public void AppendEnumerable(IEnumerable enumerable)
        {
            int startLength = _builder.Length;

            // If the return is enumerable, then we'll convert to semi-colon delimited elements
            // each of which must be converted, so we'll recurse for each element
            foreach (object element in enumerable)
            {
                if (_builder.Length > startLength)
                {
                    AppendSeparator();
                }

                // we need to convert and escape each element of the array
                AppendElement(element);
            }
        }

        private void AppendElement(object? value)
        {
            _escapeDepth++;
            Append(value);
            _escapeDepth--;
        }

        private void Append(object? value)
        {
            switch (value)
            {
                case null:
                    break;

                case string s:
                    AppendEscaped(s);
                    break;

                case IDictionary d:
                    AppendDictionary(d);
                    break;

                case Array a:
                    AppendArray(a);
                    break;

                case IEnumerable e:
                    AppendEnumerable(e);
                    break;

                default:
                    AppendEscaped(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private void AppendEscaped(string? value)
            => _builder.AppendEscaped(value, _escapeDepth);

        private void AppendSeparator()
            => _builder.AppendEscaped(';', _escapeDepth);
    }
}
