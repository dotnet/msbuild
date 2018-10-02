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
using System.Text;

namespace Microsoft.Build.Shared
{
    internal static class StringBuilderCache
    {
        // The value 360 was chosen in discussion with performance experts as a compromise between using
        // as little memory (per thread) as possible and still covering a large part of short-lived
        // StringBuilder creations on the startup path of VS designers.
        private const int MAX_BUILDER_SIZE = 360;

        [ThreadStatic]
        private static StringBuilder t_cachedInstance;

        public static StringBuilder Acquire(int capacity = 16 /*StringBuilder.DefaultCapacity*/)
        {
            if (capacity <= MAX_BUILDER_SIZE)
            {
                StringBuilder sb = StringBuilderCache.t_cachedInstance;
                if (sb != null)
                {
                    // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        StringBuilderCache.t_cachedInstance = null;
                        sb.Length = 0; // Equivalent of sb.Clear() that works on .Net 3.5
                        return sb;
                    }
                }
            }
            return new StringBuilder(capacity);
        }

        public static void Release(StringBuilder sb)
        {
            if (sb.Capacity <= MAX_BUILDER_SIZE)
            {
                StringBuilderCache.t_cachedInstance = sb;
            }
        }

        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            Release(sb);
            return result;
        }
    }
}
