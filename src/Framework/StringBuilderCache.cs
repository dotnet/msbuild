// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;
#if DEBUG && !CLR2COMPATIBILITY && !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
using Microsoft.Build.Eventing;
#endif

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A cached reusable instance of StringBuilder.
    /// </summary>
    /// <remarks>
    /// An optimization that reduces the number of instances of <see cref="StringBuilder"/> constructed and collected.
    /// </remarks>
    internal static class StringBuilderCache
    {
        // The value 512 was chosen empirically as 95% percentile of returning string length.
        private const int MAX_BUILDER_SIZE = 512;

        [ThreadStatic]
        private static StringBuilder t_cachedInstance;

        /// <summary>
        /// Get a <see cref="StringBuilder"/> of at least the specified capacity.
        /// </summary>
        /// <param name="capacity">The suggested starting size of this instance.</param>
        /// <returns>A <see cref="StringBuilder"/> that may or may not be reused.</returns>
        /// <remarks>
        /// It can be called any number of times; if a <see cref="StringBuilder"/> is in the cache then
        /// it will be returned and the cache emptied. Subsequent calls will return a new <see cref="StringBuilder"/>.
        ///
        /// <para>The <see cref="StringBuilder"/> instance is cached in Thread Local Storage and so there is one per thread.</para>
        /// </remarks>
        public static StringBuilder Acquire(int capacity = 16 /*StringBuilder.DefaultCapacity*/)
        {
            if (capacity <= MAX_BUILDER_SIZE)
            {
                StringBuilder sb = StringBuilderCache.t_cachedInstance;
                StringBuilderCache.t_cachedInstance = null;
                if (sb != null)
                {
                    // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        sb.Length = 0; // Equivalent of sb.Clear() that works on .Net 3.5
#if DEBUG && !CLR2COMPATIBILITY && !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
                        MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: sb.GetHashCode(), newCapacity: capacity, oldCapacity: sb.Capacity, type: "sbc-hit");
#endif
                        return sb;
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder(capacity);
#if DEBUG && !CLR2COMPATIBILITY && !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
            MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: stringBuilder.GetHashCode(), newCapacity: capacity, oldCapacity: stringBuilder.Capacity, type: "sbc-miss");
#endif
            return stringBuilder;
        }

        /// <summary>
        /// Place the specified builder in the cache if it is not too big. Unbalanced Releases are acceptable.
        /// The StringBuilder should not be used after it has
        ///            been released.
        ///            Unbalanced Releases are perfectly acceptable.It
        /// will merely cause the runtime to create a new
        /// StringBuilder next time Acquire is called.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to cache. Likely returned from <see cref="Acquire(int)"/>.</param>
        /// <remarks>
        /// The StringBuilder should not be used after it has been released.
        ///
        /// <para>
        /// Unbalanced Releases are perfectly acceptable.It
        /// will merely cause the runtime to create a new
        /// StringBuilder next time Acquire is called.
        /// </para>
        /// </remarks>
        public static void Release(StringBuilder sb)
        {
            if (sb.Capacity <= MAX_BUILDER_SIZE)
            {
                // Assert we are not replacing another string builder. That could happen when Acquire is reentered.
                // User of StringBuilderCache has to make sure that calling method call stacks do not also use StringBuilderCache.
                Debug.Assert(StringBuilderCache.t_cachedInstance == null, "Unexpected replacing of other StringBuilder.");
                StringBuilderCache.t_cachedInstance = sb;
            }
#if DEBUG && !CLR2COMPATIBILITY && !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
            MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: sb.GetHashCode(), returningCapacity: sb.Capacity, returningLength: sb.Length, type: sb.Capacity <= MAX_BUILDER_SIZE ? "sbc-return" : "sbc-discard");
#endif
        }

        /// <summary>
        /// Get a string and return its builder to the cache.
        /// </summary>
        /// <param name="sb">Builder to cache (if it's not too big).</param>
        /// <returns>The <see langword="string"/> equivalent to <paramref name="sb"/>'s contents.</returns>
        /// <remarks>
        /// Convenience method equivalent to calling <see cref="StringBuilder.ToString()"/> followed by <see cref="Release"/>.
        /// </remarks>
        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            Release(sb);
            return result;
        }
    }
}
