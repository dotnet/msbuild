// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Expansion;

internal ref struct PropertyValueConverter
{
    private const int InitialCapacity = 256;

    private readonly object? _value;
    private ValueStringBuilder _builder;
    private int _escapeDepth;

    private PropertyValueConverter(object? value)
    {
        _value = value;
        _builder = default;
        _escapeDepth = 0;
    }

    public static string ToString(object? value)
    {
        PropertyValueConverter converter = new(value);

        try
        {
            return converter.ConvertCore();
        }
        finally
        {
            converter._builder.Dispose();
        }
    }

    private string ConvertCore() => _value switch
    {
        null => string.Empty,
        string s => s,
        IDictionary d => ConvertDictionary(d),
        Array a => ConvertArray(a),
        IEnumerable e => ConvertEnumerable(e),

        // The fall back is always to just convert to a string directly.
        // Issue: https://github.com/dotnet/msbuild/issues/9757
        _ => Convert.ToString(_value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private string ConvertDictionary(IDictionary dictionary)
    {
        if (dictionary.Count > 0)
        {
            _builder = new ValueStringBuilder(InitialCapacity);
            AppendDictionary(dictionary);
            return Strings.WeakIntern(_builder.AsSpan());
        }

        return string.Empty;
    }

    private string ConvertArray(Array array)
    {
        if (array.Length == 0)
        {
            return string.Empty;
        }

        _builder = new ValueStringBuilder(InitialCapacity);
        AppendArray(array);
        return Strings.WeakIntern(_builder.AsSpan());
    }

    private string ConvertEnumerable(IEnumerable enumerable)
    {
        _builder = new ValueStringBuilder(InitialCapacity);
        AppendEnumerable(enumerable);
        return Strings.WeakIntern(_builder.AsSpan());
    }

    private void AppendDictionary(IDictionary dictionary)
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
            AppendChild(entry.Key);
            _builder.Append("=");
            AppendChild(entry.Value);
        }
    }

    private void AppendArray(Array array)
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

                AppendChild(array.GetValue(lowerBound + i));
            }

            return;
        }

        foreach (object element in array)
        {
            if (_builder.Length > startLength)
            {
                AppendSeparator();
            }

            AppendChild(element);
        }
    }

    private void AppendEnumerable(IEnumerable enumerable)
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
            AppendChild(element);
        }
    }

    private void AppendChild(object? value)
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
