// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal static class ThreadLocalNodeStateBuffer<TState> where TState : struct
{
    // Do not retain exceptionally large condition buffers on every evaluator thread.
    private const int MaxCachedBufferLength = 256;

    [ThreadStatic]
    private static TState[]? t_cachedBuffer;

    internal static TState[] Acquire(int minimumLength)
    {
        if (minimumLength <= MaxCachedBufferLength)
        {
            TState[]? buffer = t_cachedBuffer;
            t_cachedBuffer = null;
            if (buffer is not null && buffer.Length >= minimumLength)
            {
                return buffer;
            }
        }

        return new TState[minimumLength];
    }

    internal static void Release(TState[] buffer, int usedLength)
    {
        if (buffer.Length > MaxCachedBufferLength ||
            (t_cachedBuffer is not null && t_cachedBuffer.Length >= buffer.Length))
        {
            return;
        }

        Array.Clear(buffer, 0, usedLength);
        t_cachedBuffer = buffer;
    }
}
