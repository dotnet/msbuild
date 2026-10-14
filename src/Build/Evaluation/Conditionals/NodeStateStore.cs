// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal struct NodeStateStore
{
    private NodeStateLayout _layout;
    private StringExpressionNode.EvaluationState[]? _stringStates;
    private MultipleComparisonNode.EvaluationState[]? _comparisonStates;
    private bool _initialized;

    internal void Initialize(NodeStateLayout layout)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Condition node state storage is already in use.");
        }

        if (layout.StringSlotCount < 0 || layout.ComparisonSlotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layout));
        }

        _layout = layout;
        _initialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref StringExpressionNode.EvaluationState GetString(int slot)
    {
        VerifySlot(slot, _layout.StringSlotCount);
        StringExpressionNode.EvaluationState[] states = _stringStates ?? AcquireStringStates();
        return ref states[slot];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref MultipleComparisonNode.EvaluationState GetComparison(int slot)
    {
        VerifySlot(slot, _layout.ComparisonSlotCount);
        MultipleComparisonNode.EvaluationState[] states = _comparisonStates ?? AcquireComparisonStates();
        return ref states[slot];
    }

    internal void Release()
    {
        StringExpressionNode.EvaluationState[]? stringStates = _stringStates;
        MultipleComparisonNode.EvaluationState[]? comparisonStates = _comparisonStates;
        NodeStateLayout layout = _layout;
        this = default;

        if (stringStates is not null)
        {
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(stringStates, layout.StringSlotCount);
        }

        if (comparisonStates is not null)
        {
            ThreadLocalNodeStateBuffer<MultipleComparisonNode.EvaluationState>.Release(comparisonStates, layout.ComparisonSlotCount);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private StringExpressionNode.EvaluationState[] AcquireStringStates() =>
        _stringStates = ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(_layout.StringSlotCount);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private MultipleComparisonNode.EvaluationState[] AcquireComparisonStates() =>
        _comparisonStates = ThreadLocalNodeStateBuffer<MultipleComparisonNode.EvaluationState>.Acquire(_layout.ComparisonSlotCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void VerifySlot(int slot, int count)
    {
        if (!_initialized || (uint)slot >= (uint)count)
        {
            ThrowInvalidSlot(slot);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowInvalidSlot(int slot)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Condition node state storage is not initialized.");
        }

        throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
