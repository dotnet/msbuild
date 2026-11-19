// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Buffers;

namespace Microsoft.Build.Utilities;

internal static partial class BufferPool<T>
{
    /// <summary>
    ///  An array pool that caches one buffer per power-of-two size from 16 through 256 elements per thread
    ///  in front of <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <remarks>
    ///  On .NET Framework, <see cref="ArrayPool{T}.Shared"/> takes a lock on every call and has no per-thread
    ///  cache. Cached buffers are retained for the lifetime of the thread.
    /// </remarks>
    private sealed class SharedArrayPool : ArrayPool<T>
    {
        // A T[][] cache is covariant, so storing a buffer can require a runtime check
        // that it matches the outer array's actual element type. Arrays of structs
        // are not covariant; updating this field through a ref to a CachedArray slot
        // avoids that array-element store check (but still requires a GC write barrier).
        private struct CachedArray
        {
            public T[]? Array;
        }

        // ArrayPool.Shared rounds these lengths to power-of-two buckets starting at 16.
        private const int MinCachedLength = 16;
        private const int MaxCachedLength = 256;
        private const int BucketCount = 5;

        // Each closed BufferPool<T> gets a separate cache for each thread.
        [ThreadStatic]
        private static CachedArray[]? t_cachedArrays;

        private readonly ArrayPool<T> _sharedPool = Shared;

        public override T[] Rent(int minimumLength)
        {
            // Only requests covered by our cache can be satisfied here. The matching
            // bucket always contains arrays large enough for the request.
            if (minimumLength is > 0 and <= MaxCachedLength && t_cachedArrays is { } cachedArrays)
            {
                int bucketIndex = GetBucketIndex(minimumLength);
                ref CachedArray slot = ref cachedArrays[bucketIndex];
                if (slot.Array is { } cachedArray)
                {
                    // Remove the buffer before handing it out so it cannot be rented twice.
                    slot.Array = null;
                    return cachedArray;
                }
            }

            // Cache miss or unsupported size: use the framework's shared pool.
            return _sharedPool.Rent(minimumLength);
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            ArgumentNullException.ThrowIfNull(array);

            // Keep ArrayPool.Shared's own validation for lengths outside our buckets.
            if (array.Length is < MinCachedLength or > MaxCachedLength || (array.Length & (array.Length - 1)) != 0)
            {
                _sharedPool.Return(array, clearArray);
                return;
            }

            // Clear before caching; a later renter may bypass ArrayPool.Shared.
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            // Allocate this thread's bucket slots only when a cacheable buffer is returned.
            CachedArray[] cachedArrays = t_cachedArrays ??= new CachedArray[BucketCount];
            int bucketIndex = GetBucketIndex(array.Length);
            ref CachedArray slot = ref cachedArrays[bucketIndex];
            T[]? cachedArray = slot.Array;
            slot.Array = array;

            // Keep the most recently returned buffer in the thread-local slot and
            // send the displaced buffer to the shared pool.
            if (cachedArray is not null)
            {
                _sharedPool.Return(cachedArray);
            }
        }

        private static int GetBucketIndex(int length) => length switch
        {
            // Each range maps to the smallest cached buffer that can satisfy it.
            <= 16 => 0,
            <= 32 => 1,
            <= 64 => 2,
            <= 128 => 3,
            _ => 4,
        };
    }
}
#endif
