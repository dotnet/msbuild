// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

/// <summary>
/// The bounded window of original, ordinary string records addressable by version 30 string slices.
/// These limits and insertion rules are part of the experimental format.
/// </summary>
internal sealed class StringSliceSourceCache
{
    internal const int Capacity = 8;
    internal const int MinimumLength = 4096;
    internal const int MaximumLength = 1024 * 1024;

    private readonly (int RecordId, string? Text)[] _sources = new (int, string?)[Capacity];
    private int _next;

    internal void Add(int recordId, string text)
    {
        if (text.Length is < MinimumLength or > MaximumLength)
        {
            return;
        }

        _sources[_next] = (recordId, text);
        _next = (_next + 1) % Capacity;
    }

    internal string? GetSource(int recordId)
    {
        foreach (var source in _sources)
        {
            if (source.RecordId == recordId)
            {
                return source.Text;
            }
        }

        return null;
    }

    internal bool TryFindSuffix(ReadOnlySpan<char> payload, out int recordId, out int start)
    {
        for (int i = 1; i <= Capacity; i++)
        {
            var source = _sources[(_next + Capacity - i) % Capacity];
            if (source.Text is not null && source.Text.Length >= payload.Length)
            {
                int offset = source.Text.Length - payload.Length;
                if (source.Text.AsSpan(offset).SequenceEqual(payload))
                {
                    recordId = source.RecordId;
                    start = offset;
                    return true;
                }
            }
        }

        recordId = 0;
        start = 0;
        return false;
    }
}
