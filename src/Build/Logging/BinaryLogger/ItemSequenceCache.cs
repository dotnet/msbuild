// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging;

internal sealed class ItemSequenceCache
{
    internal const int MinimumBytes = 32;
    internal const int MaximumSequenceBytes = 64 * 1024;
    internal const int MaximumBytes = 4 * 1024 * 1024;
    internal const int MaximumEntries = 4096;

    private readonly Dictionary<ArraySegment<byte>, int> _sequences = new(ByteComparer.Instance);

    internal int ByteCount { get; private set; }
    internal int Count => _sequences.Count;
    internal bool ResetRequired { get; private set; }

    internal bool TryGetOrAdd(ArraySegment<byte> bytes, out int id, out bool added)
    {
        added = false;
        id = 0;
        if (bytes.Count < MinimumBytes || bytes.Count > MaximumSequenceBytes)
        {
            return false;
        }

        if (_sequences.TryGetValue(bytes, out id))
        {
            return true;
        }

        if (Count == MaximumEntries || ByteCount + bytes.Count > MaximumBytes)
        {
            ResetRequired = true;
            return false;
        }

        id = Count;
        _sequences.Add(new ArraySegment<byte>(bytes.AsSpan().ToArray()), id);
        ByteCount += bytes.Count;
        added = true;
        return true;
    }

    internal void Clear()
    {
        _sequences.Clear();
        ByteCount = 0;
        ResetRequired = false;
    }

    internal sealed class ByteComparer : IEqualityComparer<ArraySegment<byte>>
    {
        internal static readonly ByteComparer Instance = new();

        public bool Equals(ArraySegment<byte> x, ArraySegment<byte> y) => x.AsSpan().SequenceEqual(y.AsSpan());

        public int GetHashCode(ArraySegment<byte> bytes)
        {
            uint hash = 2166136261;
            foreach (byte value in bytes.AsSpan())
            {
                hash = unchecked((hash ^ value) * 16777619);
            }

            return unchecked((int)hash);
        }
    }
}
