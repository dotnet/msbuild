// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
///  A cached reusable instance of <see cref="StringBuilder"/>.
/// </summary>
/// <remarks>
///  An optimization that reduces the number of instances of <see cref="StringBuilder"/> constructed and collected.
/// </remarks>
internal static class StringBuilderCache
{
    // The value 512 was chosen empirically as 95% percentile of returning string length.
    private const int MAX_BUILDER_SIZE = 512;

    [ThreadStatic]
    private static StringBuilder? t_cachedInstance;

    /// <summary>
    ///  Get a <see cref="StringBuilder"/> of at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The suggested starting size of this instance.</param>
    /// <returns>
    ///  A <see cref="StringBuilder"/> that may or may not be reused.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   Can be called any number of times. If a <see cref="StringBuilder"/> is in the cache then
    ///   it will be returned and the cache emptied. Subsequent calls will return a new <see cref="StringBuilder"/>.
    ///  </para>
    ///  <para>
    ///   The <see cref="StringBuilder"/> instance is cached in Thread Local Storage and so there is one per thread.
    ///  </para>
    /// </remarks>
    public static StringBuilder Acquire(int capacity = 16 /* StringBuilder.DefaultCapacity */)
    {
        StringBuilder? builder;

        if (capacity <= MAX_BUILDER_SIZE)
        {
            builder = t_cachedInstance;
            t_cachedInstance = null;

            Debug.Assert(
                builder is null || builder.Capacity <= MAX_BUILDER_SIZE,
                $"How did we get a builder with a capacity larger than MAX_BUILDER_SIZE?");

            // Avoid StringBuilder block fragmentation by getting a new StringBuilder
            // when the requested size is larger than the current capacity
            if (builder?.Capacity >= capacity)
            {
                builder.Clear();
                LogAcquire(builder, capacity, cacheHit: true);

                return builder;
            }
        }

        builder = new StringBuilder(capacity);
        LogAcquire(builder, capacity, cacheHit: false);

        return builder;
    }

    /// <summary>
    ///  Place the specified builder in the cache if it is not too big. Unbalanced Releases are acceptable.
    ///  The StringBuilder should not be used after it has been released. Unbalanced Releases are perfectly acceptable.
    ///  It will merely cause the runtime to create a new <see cref="StringBuilder"/> next time Acquire is called.
    /// </summary>
    /// <param name="builder">
    ///  The <see cref="StringBuilder"/> to cache. Likely returned from <see cref="Acquire(int)"/>.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   The <see cref="StringBuilder"/> should not be used after it has been released.
    ///  </para>
    ///  <para>
    ///   Unbalanced releases are perfectly acceptable. It will merely cause the runtime to create a new
    ///   <see cref="StringBuilder"/> next time Acquire is called.
    ///  </para>
    /// </remarks>
    public static void Release(StringBuilder builder)
    {
        if (builder.Capacity <= MAX_BUILDER_SIZE)
        {
            // Assert we are not replacing another string builder. That could happen when Acquire is reentered.
            // User of StringBuilderCache has to make sure that calling method call stacks do not also use StringBuilderCache.
            Debug.Assert(t_cachedInstance is null, "Unexpected replacing of other StringBuilder.");
            t_cachedInstance = builder;
        }

        LogRelease(builder);
    }

    /// <summary>
    ///  Get a string and return its builder to the cache.
    /// </summary>
    /// <param name="builder">
    ///  <see cref="StringBuilder"/> to cache (if it's not too big).
    /// </param>
    /// <returns>
    ///  The <see langword="string"/> equivalent to <paramref name="builder"/>'s contents.
    /// </returns>
    /// <remarks>
    ///  Convenience method equivalent to calling <see cref="StringBuilder.ToString()"/> followed by <see cref="Release"/>.
    /// </remarks>
    public static string GetStringAndRelease(StringBuilder builder)
    {
        string result = builder.ToString();
        Release(builder);
        return result;
    }

    [Conditional("DEBUG")]
    private static void LogAcquire(StringBuilder builder, int newCapacity, bool cacheHit)
        => Eventing.MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(
            hash: builder.GetHashCode(),
            newCapacity: newCapacity,
            oldCapacity: builder.Capacity,
            type: cacheHit ? "sbc-hit" : "sbc-miss");

    [Conditional("DEBUG")]
    private static void LogRelease(StringBuilder builder)
        => Eventing.MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(
            hash: builder.GetHashCode(),
            returningCapacity: builder.Capacity,
            returningLength: builder.Length,
            type: builder.Capacity <= MAX_BUILDER_SIZE ? "sbc-return" : "sbc-discard");
}
