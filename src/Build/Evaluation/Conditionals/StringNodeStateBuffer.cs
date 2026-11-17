// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using StringNodeState = Microsoft.Build.Evaluation.NodeStateStore.StringNodeState;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Supplies exclusively borrowed string-node state buffers, with bounded shared pooling.
/// </summary>
internal static class StringNodeStateBuffer
{
    // Do not pool exceptionally large condition-state buffers.
    private const int MaxPooledBufferLength = 4096;

    internal static StringNodeState[] Acquire(int minimumLength)
    {
        if (minimumLength <= MaxPooledBufferLength)
        {
            return ArrayPool<StringNodeState>.Shared.Rent(minimumLength);
        }

        return new StringNodeState[minimumLength];
    }

    internal static void Release(StringNodeState[] buffer)
    {
        if (buffer.Length <= MaxPooledBufferLength)
        {
            ArrayPool<StringNodeState>.Shared.Return(buffer, clearArray: true);
        }
    }
}
