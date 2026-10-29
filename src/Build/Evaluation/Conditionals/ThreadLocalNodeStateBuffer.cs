// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Threading;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal static class ThreadLocalNodeStateBuffer<TState> where TState : struct
{
    // Do not retain exceptionally large condition buffers on every evaluator thread.
    private const int MaxCachedBufferLength = 256;
    private const int MaxPooledBufferLength = 4096;

    // Bound oversized retention across threads rather than keeping large per-thread buffers.
    private static ArrayPool<TState>? s_oversizedPool;

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
        else if (minimumLength <= MaxPooledBufferLength)
        {
            ArrayPool<TState> pool = LazyInitializer.EnsureInitialized(
                ref s_oversizedPool,
                static () => ArrayPool<TState>.Create(MaxPooledBufferLength, maxArraysPerBucket: 4))!;
            return pool.Rent(minimumLength);
        }

        return new TState[minimumLength];
    }

    internal static void Release(TState[] buffer, int usedLength)
    {
        if (buffer.Length > MaxCachedBufferLength)
        {
            if (buffer.Length <= MaxPooledBufferLength)
            {
                s_oversizedPool!.Return(buffer, clearArray: true);
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
