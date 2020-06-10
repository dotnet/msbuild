// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build
{
    /// <summary>
    /// A cache of weak GC handles pointing to strings. Weak GC handles are functionally equivalent to WeakReference's but have less overhead
    /// (they're a struct as opposed to WR which is a finalizable class) at the expense of requiring manual lifetime management. As long as
    /// a string has an ordinary strong GC root elsewhere in the process and another string with the same hashcode hasn't reused the entry,
    /// the cache has a reference to it and can match it to an internable. When the string is collected, it is also automatically "removed"
    /// from the cache by becoming unrecoverable from the GC handle. GC handles that do not reference a live string anymore are freed lazily.
    /// </summary>
    internal sealed partial class WeakStringCache : IDisposable
    {
        /// <summary>
        /// Debug stats returned by GetDebugInfo().
        /// </summary>
        public struct DebugInfo
        {
            public int LiveStringCount;
            public int CollectedStringCount;
        }

        /// <summary>
        /// Holds a weak GC handle to a string. Shared by all strings with the same hash code and referencing the last such string we've seen.
        /// </summary>
        private struct StringWeakHandle
        {
            /// <summary>
            /// Weak GC handle to the last string of the given hashcode we've seen.
            /// </summary>
            public GCHandle WeakHandle;

            /// <summary>
            /// Returns true if the string referenced by the handle is still alive.
            /// </summary>
            public bool IsUsed => WeakHandle.Target != null;

            /// <summary>
            /// Returns the string referenced by this handle if it is equal to the given internable.
            /// </summary>
            /// <param name="internable">The internable describing the string we're looking for.</param>
            /// <returns>The string matching the internable or null if the handle is referencing a collected string or the string is different.</returns>
            public string GetString<T>(T internable) where T : IInternable
            {
                if (WeakHandle.IsAllocated && WeakHandle.Target is string str)
                {
                    if (internable.Length == str.Length &&
                        internable.StartsWithStringByOrdinalComparison(str))
                    {
                        return str;
                    }
                }
                return null;
            }

            /// <summary>
            /// Sets the handle to the given string. If the handle is still referencing another live string, that string is effectively forgotten.
            /// </summary>
            /// <param name="str">The string to set.</param>
            public void SetString(string str)
            {
                if (!WeakHandle.IsAllocated)
                {
                    // The handle is not allocated - allocate it.
                    WeakHandle = GCHandle.Alloc(str, GCHandleType.Weak);
                }
                else
                {
                    WeakHandle.Target = str;
                }
            }

            /// <summary>
            /// Frees the GC handle.
            /// </summary>
            public void Free()
            {
                WeakHandle.Free();
            }
        }

        /// <summary>
        /// Initial capacity of the underlying dictionary.
        /// </summary>
        private const int _initialCapacity = 503;

        /// <summary>
        /// The maximum size we let the collection grow before scavenging unused entries.
        /// </summary>
        private int _scavengeThreshold = _initialCapacity;

        /// <summary>
        /// Implements the simple yet very decently performing djb2 hash function (xor version).
        /// </summary>
        /// <param name="internable">The internable to compute the hash code for.</param>
        /// <returns>The 32-bit hash code.</returns>
        internal static int GetInternableHashCode<T>(T internable) where T : IInternable
        {
            int hashCode = 5381;
            for (int i = 0; i < internable.Length; i++)
            {
                unchecked
                {
                    hashCode = hashCode * 33 ^ internable[i];
                }
            }
            return hashCode;
        }

        /// <summary>
        /// Frees all GC handles and clears the cache.
        /// </summary>
        public void Dispose()
        {
            foreach (KeyValuePair<int, StringWeakHandle> entry in _stringsByHashCode)
            {
                entry.Value.Free();
            }
            _stringsByHashCode.Clear();
        }

        /// <summary>
        /// Returns internal debug counters calculated based on the current state of the cache.
        /// </summary>
        private DebugInfo GetDebugInfoImpl()
        {
            DebugInfo debugInfo = new DebugInfo();

            foreach (KeyValuePair<int, StringWeakHandle> entry in _stringsByHashCode)
            {
                if (entry.Value.IsUsed)
                {
                    debugInfo.LiveStringCount++;
                }
                else
                {
                    debugInfo.CollectedStringCount++;
                }
            }

            return debugInfo;
        }
    }
}
