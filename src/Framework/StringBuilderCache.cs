// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
** Purpose: provide a cached reusable instance of StringBuilder
**          per thread  it's an optimization that reduces the
**          number of instances constructed and collected.
**
**  Acquire - is used to get a string builder to use of a
**            particular size.  It can be called any number of
**            times, if a StringBuilder is in the cache then
**            it will be returned and the cache emptied.
**            subsequent calls will return a new StringBuilder.
**
**            A StringBuilder instance is cached in
**            Thread Local Storage and so there is one per thread
**
**  Release - Place the specified builder in the cache if it is
**            not too big.
**            The StringBuilder should not be used after it has
**            been released.
**            Unbalanced Releases are perfectly acceptable.  It
**            will merely cause the runtime to create a new
**            StringBuilder next time Acquire is called.
**
**  GetStringAndRelease
**          - ToString() the StringBuilder, Release it to the
**            cache and return the resulting string
**
===========================================================*/

using System;
using System.Diagnostics;
using System.Text;
#if !CLR2COMPATIBILITY && !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
using Microsoft.Build.Eventing;
#endif

namespace Microsoft.Build.Framework
{
    internal static class StringBuilderCache
    {
        // The value 512 was chosen empirically as 99% percentile by captured data.
        private const int MAX_BUILDER_SIZE = 512;

        [ThreadStatic]
        private static StringBuilder t_cachedInstance;

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
            MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: sb.GetHashCode(), returningCapacity: sb.Capacity, returningLength: sb.Length, type: sb.Capacity <= MAX_BUILDER_SIZE ? "sbc-return" :  "sbc-discard");
#endif
        }

        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            Release(sb);
            return result;
        }
    }
}
