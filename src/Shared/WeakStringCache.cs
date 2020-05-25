// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if !CLR2COMPATIBILITY
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build
{
    /// <summary>
    /// A cache of weak GC handles (equivalent to weak references) pointing to strings. As long as a string has an ordinary strong GC root
    /// elsewhere in the process and another string with the same hashcode hasn't reused the entry, the cache has a reference to it and can
    /// match it to an internable. When the string is collected, it is also automatically "removed" from the cache by becoming unrecoverable
    /// from the GC handle. GC handles that do not reference a live string anymore are freed lazily.
    /// </summary>
    internal sealed class WeakStringCache : IDisposable
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
        /// Holds a weak GC handle to a string. Shared among all strings with the same hash code and referencing the last one we've seen.
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
            /// Sets the handle to the given string. If the handle is still referencing another live string, it is effectively forgotten.
            /// </summary>
            /// <param name="str">The string to add.</param>
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
        /// Improvised capacity for the scavenging heuristics.
        /// </summary>
        private int _capacity = 101;

        /// <summary>
        /// A dictionary mapping string hash codes to weak GC handles.
        /// </summary>
#if CLR2COMPATIBILITY
        private Dictionary<int, StringWeakHandle> _stringsByHashCode;
#else
        private ConcurrentDictionary<int, StringWeakHandle> _stringsByHashCode;
#endif

        public WeakStringCache()
        {
#if CLR2COMPATIBILITY
            _stringsByHashCode = new Dictionary<int, StringWeakHandle>(_capacity);
#else
            _stringsByHashCode = new ConcurrentDictionary<int, StringWeakHandle>(Environment.ProcessorCount, _capacity);
#endif
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

            StringWeakHandle handle;
            string result;
#if !CLR2COMPATIBILITY
            bool addNewHandle = false;
#endif

#if CLR2COMPATIBILITY
            lock (_stringsByHashCode)
#endif
            {
                if (_stringsByHashCode.TryGetValue(hashCode, out handle))
                {
                    result = handle.GetString(internable);
                    if (result != null)
                    {
                        cacheHit = true;
                        return result;
                    }
                }
                else
                {
                    handle = new StringWeakHandle();
#if !CLR2COMPATIBILITY
                    addNewHandle = true;
#endif
                }
            }

            // We don't have the string in the dictionary - create it.
            result = internable.ExpensiveConvertToString();

#if CLR2COMPATIBILITY
            lock (_stringsByHashCode)
            {
                // Re-read the handle under the lock to prevent leaks if somebody else has modified it.
                if (_stringsByHashCode.TryGetValue(hashCode, out StringWeakHandle newHandle))
                {
                    handle = newHandle;
                }
#else
            {
#endif
                handle.SetString(result);

                // Prevent the dictionary from growing forever with GC handles that don't reference any live strings anymore.
                if (_stringsByHashCode.Count >= _capacity)
                {
                    // Get rid of unused handles.
                    ScavengeNoLock();
                    // And do this again when the number of handles reaches double the current after-scavenge number.
                    _capacity = _stringsByHashCode.Count * 2;
                }

#if CLR2COMPATIBILITY
                _stringsByHashCode[hashCode] = handle;
#else
                if (addNewHandle && !_stringsByHashCode.TryAdd(hashCode, handle))
                {
                    // If somebody beat us to it and the new handle has not been added, free it.
                    handle.Free();
                }
#endif
            }

            cacheHit = false;
            return result;
        }

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
        /// Iterates over the cache and removes unused GC handles, i.e. handles that don't reference live strings.
        /// This is expensive so try to call such that the cost is amortized to O(1) per GetOrCreateEntry() invocation.
        /// Assumes lock-free access to the dictionary, i.e. the lock is taken or the dictionary supports concurrency.
        /// </summary>
        private void ScavengeNoLock()
        {
#if CLR2COMPATIBILITY
            List<int> keysToRemove = null;
            foreach (KeyValuePair<int, StringWeakHandle> entry in _stringsByHashCode)
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
#else
            foreach (KeyValuePair<int, StringWeakHandle> entry in _stringsByHashCode)
            {
                if (!entry.Value.IsUsed && _stringsByHashCode.TryRemove(entry.Key, out StringWeakHandle removedHandle))
                {
                    // Note that the removed handle may be different from the one we got from the enumerator so it
                    // is possible that it is actually used. We are fine with these races as long as
                    // 1) We never leak handles.
                    // 2) The data structures remain consistent internally.
                    removedHandle.Free();
                }
            }
#endif
        }

        /// <summary>
        /// Public version of ScavengeUnderLock() which takes the lock.
        /// </summary>
        public void Scavenge()
        {
#if CLR2COMPATIBILITY
            lock (_stringsByHashCode)
#endif
            {
                ScavengeNoLock();
            }
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
        public DebugInfo GetDebugInfo()
        {
            DebugInfo debugInfo = new DebugInfo();

#if CLR2COMPATIBILITY
            lock (_stringsByHashCode)
#endif
            {
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
            }

            return debugInfo;
        }
    }
}
