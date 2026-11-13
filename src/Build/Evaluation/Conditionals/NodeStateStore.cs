// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Owns mutable node state for one active condition evaluation.
/// </summary>
/// <remarks>
/// Initialize from the parsed layout and release afterward.
/// </remarks>
internal struct NodeStateStore
{
    private NodeStateLayout _layout;
    private StringExpressionNode.EvaluationState[]? _stringStates;
    private bool _initialized;

    internal void Initialize(NodeStateLayout layout)
    {
        Assumed.False(_initialized, "Condition node state storage is not initialized.");

        Assumed.PositiveOrZero(layout.StringSlotCount);

        _layout = layout;
        _initialized = true;
    }

    internal ref StringExpressionNode.EvaluationState GetString(int slot)
    {
        if (!_initialized || (uint)slot >= (uint)_layout.StringSlotCount)
        {
            ThrowInvalidSlot(slot);
        }

        StringExpressionNode.EvaluationState[] states = _stringStates ?? AcquireStringStates();
        return ref states[slot];
    }

    internal void Release()
    {
        StringExpressionNode.EvaluationState[]? stringStates = _stringStates;
        NodeStateLayout layout = _layout;
        this = default;

        if (stringStates is not null)
        {
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(stringStates, layout.StringSlotCount);
        }
    }

    private StringExpressionNode.EvaluationState[] AcquireStringStates() =>
        _stringStates = ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(_layout.StringSlotCount);

    private readonly void ThrowInvalidSlot(int slot)
    {
        Assumed.True(_initialized, "Condition node state storage is not initialized.");

        Assumed.Unreachable($"Invalid {nameof(slot)} value: {slot}");
    }
}
