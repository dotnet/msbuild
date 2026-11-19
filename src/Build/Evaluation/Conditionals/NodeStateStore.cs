// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Owns mutable node state for one active condition evaluation.
/// </summary>
/// <remarks>
/// Initialize from the parsed layout and release afterward.
/// </remarks>
internal struct NodeStateStore
{
    /// <summary>
    /// Holds a string node's cached expansion; null means it has not been expanded yet.
    /// </summary>
    internal struct StringNodeState
    {
        public string? ExpandedValue;
    }

    // Covers real conditions: the largest found in the SDK, Visual Studio and NuGet packages needs 78 slots.
    // Larger buffers go back to Shared, so an unusually large condition cannot pin a big buffer on every thread.
    private const int MaxThreadCachedLength = 256;

    // On .NET Framework, ArrayPool<T>.Shared locks on every Rent and Return and has no per-thread cache,
    // which made parallel evaluation about 2x slower. So each thread keeps one buffer,
    // and Shared is used only when that buffer is missing, in use or too small.
    [ThreadStatic]
    private static StringNodeState[]? t_cachedStringStates;

    private NodeStateLayout _layout;
    private StringNodeState[]? _stringStates;
    private bool _initialized;

    internal void Initialize(NodeStateLayout layout)
    {
        Assumed.False(_initialized, "Condition node state storage is not initialized.");

        Assumed.PositiveOrZero(layout.StringSlotCount);

        _layout = layout;
        _initialized = true;
    }

    internal ref StringNodeState GetString(int slot)
    {
        if (!_initialized || (uint)slot >= (uint)_layout.StringSlotCount)
        {
            ThrowInvalidSlot(slot);
        }

        StringNodeState[] states = _stringStates ??= RentStringStates(_layout.StringSlotCount);
        return ref states[slot];
    }

    internal void Release()
    {
        StringNodeState[]? stringStates = _stringStates;
        int usedLength = _layout.StringSlotCount;
        this = default;

        if (stringStates is not null)
        {
            ReturnStringStates(stringStates, usedLength);
        }
    }

    private static StringNodeState[] RentStringStates(int length)
    {
        StringNodeState[]? cached = t_cachedStringStates;
        if (cached is not null && cached.Length >= length)
        {
            t_cachedStringStates = null;
            return cached;
        }

        return ArrayPool<StringNodeState>.Shared.Rent(length);
    }

    private static void ReturnStringStates(StringNodeState[] states, int usedLength)
    {
        // Only slots below the layout's slot count can be written.
        Array.Clear(states, 0, usedLength);

        StringNodeState[]? cached = t_cachedStringStates;
        if (states.Length <= MaxThreadCachedLength && (cached is null || cached.Length < states.Length))
        {
            t_cachedStringStates = states;
            if (cached is not null)
            {
                ArrayPool<StringNodeState>.Shared.Return(cached);
            }

            return;
        }

        ArrayPool<StringNodeState>.Shared.Return(states);
    }

    private readonly void ThrowInvalidSlot(int slot)
    {
        Assumed.True(_initialized, "Condition node state storage is not initialized.");

        Assumed.Unreachable($"Invalid {nameof(slot)} value: {slot}");
    }
}
