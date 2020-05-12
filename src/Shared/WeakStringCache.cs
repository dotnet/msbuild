// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build
{
    /// <summary>
    /// A cache of weak GC handles (equivalent to weak references) pointing to strings. As long as a string has an ordinary strong GC root
    /// elsewhere in the process, the cache has a reference to it and can match it to an internable. When the string is collected, it is
    /// also automatically "removed" from the cache by becoming unrecoverable from the GC handle. Buckets of GC handles that do not
    /// reference any live strings anymore are freed lazily.
    /// </summary>
    internal sealed class WeakStringCache : IDisposable
    {
        /// <summary>
        /// Debug stats returned by GetDebugInfo().
        /// </summary>
        public struct DebugInfo
        {
            public int UsedBucketCount;
            public int UnusedBucketCount;
            public int LiveStringCount;
            public int CollectedStringCount;
            public int HashCollisionCount;
        }

        /// <summary>
        /// Holds weak GC handles to one or more strings that share the same hash code.
        /// </summary>
        private struct StringBucket
        {
            /// <summary>
            /// Weak GC handle to the first string of the given hashcode we've seen.
            /// </summary>
            public GCHandle WeakHandle;

            /// <summary>
            /// Overflow area used for additional strings sharing the same hash code. Since hash collisions should
            /// to be very rare, this field is expected to be null in most buckets.
            /// </summary>
            public List<GCHandle> WeakHandleOverflow;

            /// <summary>
            /// Returns true if and only if this bucket contains a handle to at least one live string.
            /// </summary>
            public bool IsUsed
            {
                get
                {
                    if (WeakHandle.Target != null)
                    {
                        return true;
                    }
                    if (WeakHandleOverflow != null)
                    {
                        for (int i = 0; i < WeakHandleOverflow.Count; i++)
                        {
                            if (WeakHandleOverflow[i].Target != null)
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
            }

            /// <summary>
            /// Gets the number of GC handles allocated by this bucket.
            /// </summary>
            public int HandleCount
            {
                get
                {
                    return 1 + (WeakHandleOverflow != null ? WeakHandleOverflow.Count : 0);
                }
            }

            /// <summary>
            /// Gets the number of GC handles allocated by this bucket and referencing live strings.
            /// </summary>
            public int LiveHandleCount
            {
                get
                {
                    int liveCount = 0;
                    if (WeakHandle.Target != null)
                    {
                        liveCount++;
                    }
                    if (WeakHandleOverflow != null)
                    {
                        for (int i = 0; i < WeakHandleOverflow.Count; i++)
                        {
                            if (WeakHandleOverflow[i].Target != null)
                            {
                                liveCount++;
                            }
                        }
                    }
                    return liveCount;
                }
            }

            /// <summary>
            /// Returns the string referenced by this bucket that is equal to the given internable.
            /// </summary>
            /// <param name="internable">The internable describing the string we're looking for.</param>
            /// <returns>The string matching the internable or null if no such string exists.</returns>
            public string GetString<T>(T internable) where T : IInternable
            {
                if (WeakHandle.IsAllocated && WeakHandle.Target is string baseString)
                {
                    if (internable.Length == baseString.Length &&
                        internable.StartsWithStringByOrdinalComparison(baseString))
                    {
                        return baseString;
                    }
                }
                if (WeakHandleOverflow != null)
                {
                    for (int i = 0; i < WeakHandleOverflow.Count; i++)
                    {
                        if (WeakHandleOverflow[i].Target is string extendedString)
                        {
                            if (internable.Length == extendedString.Length &&
                                internable.StartsWithStringByOrdinalComparison(extendedString))
                            {
                                return extendedString;
                            }
                        }
                    }
                }
                return null;
            }

            /// <summary>
            /// Adds a string to this bucket, reusing one of the existing weak GC handles if they reference an already collected string.
            /// </summary>
            /// <param name="str">The string to add.</param>
            public void AddString(string str)
            {
                if (!WeakHandle.IsAllocated)
                {
                    // The main handle is not allocated - allocate it.
                    WeakHandle = GCHandle.Alloc(str, GCHandleType.Weak);
                }
                else if (WeakHandle.Target == null)
                {
                    // The main handle is allocated but the target has been collected - reuse it.
                    WeakHandle.Target = str;
                }
                else if (WeakHandleOverflow == null)
                {
                    // The overflow area is not initialized - initialize it and add a new handle.
                    // Note that collisions are rare so we start with a very conservative capacity of 2.
                    WeakHandleOverflow = new List<GCHandle>(2);
                    WeakHandleOverflow.Add(GCHandle.Alloc(str, GCHandleType.Weak));
                }
                else
                {
                    // Find the first usable slot in the overflow area or add a new slot if there is none.
                    bool foundExistingSlot = false;
                    for (int i = 0; i < WeakHandleOverflow.Count; i++)
                    {
                        if (WeakHandleOverflow[i].Target == null)
                        {
                            GCHandle handle = WeakHandleOverflow[i];
                            handle.Target = str;
                            WeakHandleOverflow[i] = handle;
                            foundExistingSlot = true;
                            break;
                        }
                    }
                    if (!foundExistingSlot)
                    {
                        WeakHandleOverflow.Add(GCHandle.Alloc(str, GCHandleType.Weak));
                    }
                }
            }

            /// <summary>
            /// Frees all GC handles allocated in this bucket.
            /// </summary>
            public void Free()
            {
                WeakHandle.Free();
                if (WeakHandleOverflow != null)
                {
                    for (int i = 0; i < WeakHandleOverflow.Count; i++)
                    {
                        WeakHandleOverflow[i].Free();
                    }
                }
            }
        }

        /// <summary>
        /// Improvised capacity for the scavenging heuristics.
        /// </summary>
        private int _capacity = 100;

        /// <summary>
        /// A dictionary mapping string hash codes to string buckets holding the actual weak GC handles.
        /// </summary>
        private Dictionary<int, StringBucket> _stringsByHashCode;

        public WeakStringCache()
        {
            _stringsByHashCode = new Dictionary<int, StringBucket>(_capacity);
        }

        /// <summary>
        /// Main entrypoint of this cache. Tries to look up a string that matches the given internable. If it succeeds, returns
        /// the string and sets cacheHit to true. If the string is not found, calls ExpensiveConvertToString on the internable,
        /// adds the resulting string to the cache, and returns it setting cacheHit to false.
        /// </summary>
        /// <param name="internable">The internable describing the string we're looking for.</param>
        /// <returns>A string matching the given internable.</returns>
        public string GetOrCreateEntry<T>(T internable, out bool cacheHit) where T : IInternable
        {
            int hashCode = GetInternableHashCode(internable);

            StringBucket bucket;
            string result;
            lock (_stringsByHashCode)
            {
                if (!_stringsByHashCode.TryGetValue(hashCode, out bucket))
                {
                    bucket = new StringBucket();
                }
                result = bucket.GetString(internable);
                if (result != null)
                {
                    cacheHit = true;
                    return result;
                }
            }

            // We don't have the string in the dictionary - create it.
            result = internable.ExpensiveConvertToString();

            lock (_stringsByHashCode)
            {
                bucket.AddString(result);

                // Prevent the dictionary from growing forever with buckets whose underlying GC handles
                // don't reference any live strings anymore.
                if (_stringsByHashCode.Count >= _capacity)
                {
                    // Get rid of unused buckets.
                    Scavenge();
                    // And do this again if the number of buckets reaches double the current after-scavenge number.
                    _capacity = _stringsByHashCode.Count * 2;
                }
                _stringsByHashCode[hashCode] = bucket;
            }
            cacheHit = false;
            return result;
        }

        /// <summary>
        /// Implements the simple yet very decently performing djb2 hash function.
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
                    hashCode = hashCode * 33 + internable[i];
                }
            }
            return hashCode;
        }

        /// <summary>
        /// Iterates over the cache and removes unused buckets, i.e. buckets that don't reference live strings.
        /// This is expensive so try to call such that the cost is amortized to O(1) per GetOrCreateEntry() invocation.
        /// Assumes exclusive access to the dictionary, i.e. the lock is taken.
        /// </summary>
        public void Scavenge()
        {
            List<int> keysToRemove = null;
            foreach (KeyValuePair<int, StringBucket> entry in _stringsByHashCode)
            {
                if (!entry.Value.IsUsed)
                {
                    entry.Value.Free();
                    keysToRemove ??= new List<int>();
                    keysToRemove.Add(entry.Key);
                }
            }
            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    _stringsByHashCode.Remove(keysToRemove[i]);
                }
            }
        }

        /// <summary>
        /// Frees all GC handles and clears the cache.
        /// </summary>
        public void Dispose()
        {
            foreach (KeyValuePair<int, StringBucket> entry in _stringsByHashCode)
            {
                entry.Value.Free();
            }
            _stringsByHashCode.Clear();
        }

        /// <summary>
        /// Returns internal debug counters calculated based on the current state of the cache.
        /// </summary>
        public DebugInfo GetDebugInfo()
        {
            DebugInfo debugInfo = new DebugInfo();

            lock (_stringsByHashCode)
            {
                foreach (KeyValuePair<int, StringBucket> entry in _stringsByHashCode)
                {
                    if (entry.Value.IsUsed)
                    {
                        debugInfo.UsedBucketCount++;
                    }
                    else
                    {
                        debugInfo.UnusedBucketCount++;
                    }

                    int handleCount = entry.Value.HandleCount;
                    int liveHandleCount = entry.Value.LiveHandleCount;
                    debugInfo.LiveStringCount += liveHandleCount;
                    debugInfo.CollectedStringCount += (handleCount - liveHandleCount);
                    if (handleCount > 1)
                    {
                        debugInfo.HashCollisionCount += handleCount - 1;
                    }
                }
            }

            return debugInfo;
        }
    }
}
