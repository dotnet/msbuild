// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;

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

        StringNodeState[] states = _stringStates ??= BufferPool<StringNodeState>.Shared.Rent(_layout.StringSlotCount);
        return ref states[slot];
    }

    internal void Release()
    {
        StringNodeState[]? stringStates = _stringStates;
        int usedLength = _layout.StringSlotCount;
        this = default;

        if (stringStates is not null)
        {
            // Only slots below the layout's slot count can be written.
            Array.Clear(stringStates, 0, usedLength);
            BufferPool<StringNodeState>.Shared.Return(stringStates);
        }
    }

    private readonly void ThrowInvalidSlot(int slot)
    {
        Assumed.True(_initialized, "Condition node state storage is not initialized.");

        Assumed.Unreachable($"Invalid {nameof(slot)} value: {slot}");
    }
}
