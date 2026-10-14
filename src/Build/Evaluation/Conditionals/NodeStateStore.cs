// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

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

    internal ref StringExpressionNode.EvaluationState GetString(int slot)
    {
        VerifySlot(slot, _layout.StringSlotCount);
        _stringStates ??= ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(_layout.StringSlotCount);
        return ref _stringStates[slot];
    }

    internal ref MultipleComparisonNode.EvaluationState GetComparison(int slot)
    {
        VerifySlot(slot, _layout.ComparisonSlotCount);
        _comparisonStates ??= ThreadLocalNodeStateBuffer<MultipleComparisonNode.EvaluationState>.Acquire(_layout.ComparisonSlotCount);
        return ref _comparisonStates[slot];
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

    private readonly void VerifySlot(int slot, int count)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Condition node state storage is not initialized.");
        }

        if ((uint)slot >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
