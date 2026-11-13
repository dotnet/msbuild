// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using StringNodeState = Microsoft.Build.Evaluation.NodeStateStore.StringNodeState;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Supplies exclusively borrowed string-node state buffers, with thread-local reuse and shared pooling.
/// </summary>
internal static class ThreadLocalStringNodeStateBuffer
{
    // Do not retain exceptionally large condition buffers on every evaluator thread.
    private const int MaxCachedBufferLength = 256;
    private const int MaxPooledBufferLength = 4096;

    [ThreadStatic]
    private static StringNodeState[]? t_cachedBuffer;

    internal static StringNodeState[] Acquire(int minimumLength)
    {
        if (minimumLength <= MaxCachedBufferLength)
        {
            StringNodeState[]? buffer = t_cachedBuffer;
            if (buffer is not null && buffer.Length >= minimumLength)
            {
                t_cachedBuffer = null;
                return buffer;
            }
        }
        else if (minimumLength <= MaxPooledBufferLength)
        {
            return ArrayPool<StringNodeState>.Shared.Rent(minimumLength);
        }

        return new StringNodeState[minimumLength];
    }

    internal static void Release(StringNodeState[] buffer, int usedLength)
    {
        if (buffer.Length > MaxCachedBufferLength)
        {
            if (buffer.Length <= MaxPooledBufferLength)
            {
                ArrayPool<StringNodeState>.Shared.Return(buffer, clearArray: true);
            }

            return;
        }

        if (t_cachedBuffer is not null && t_cachedBuffer.Length >= buffer.Length)
        {
            return;
        }

        Array.Clear(buffer, 0, usedLength);
        t_cachedBuffer = buffer;
    }
}
