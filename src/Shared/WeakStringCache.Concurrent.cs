
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Build
{
    /// <summary>
    /// Implements the WeakStringCache functionality on modern .NET versions where ConcurrentDictionary is available.
    /// </summary>
    internal sealed partial class WeakStringCache : IDisposable
    {
        private readonly ConcurrentDictionary<int, StringWeakHandle> _stringsByHashCode;

        public WeakStringCache()
        {
            _stringsByHashCode = new ConcurrentDictionary<int, StringWeakHandle>(Environment.ProcessorCount, _initialCapacity);
        }

        /// <summary>
        /// Main entrypoint of this cache. Tries to look up a string that matches the given internable. If it succeeds, returns
        /// the string and sets cacheHit to true. If the string is not found, calls ExpensiveConvertToString on the internable,
        /// adds the resulting string to the cache, and returns it, setting cacheHit to false.
        /// </summary>
        /// <param name="internable">The internable describing the string we're looking for.</param>
        /// <returns>A string matching the given internable.</returns>
        /// <remarks>
        /// This method performs two operations on the underlying ConcurrentDictionary on both cache hit and cache miss.
        /// 1. It checks whether the dictionary has a matching entry. The entry is temporarily removed from the cache so it doesn't
        ///    race with Scavenge() freeing GC handles. This is the first operation.
        /// 2a. If there is a matching entry, we extract the string out of it and put it back in the cache (the second operation).
        /// 2b. If there is an entry but it doesn't match, or there is no entry for the given hash code, we extract the string from
        ///     the internable, set it on the entry, and add the entry (back) in the cache.
        /// </remarks>
        public string GetOrCreateEntry<T>(T internable, out bool cacheHit) where T : IInternable
        {
            int hashCode = GetInternableHashCode(internable);

            StringWeakHandle handle;
            string result;
            bool addingNewHandle = false;

            // Get the existing handle from the cache and assume ownership by removing it. We can't use the simple TryGetValue() here because
            // the Scavenge method running on another thread could free the handle from underneath us.
            if (_stringsByHashCode.TryRemove(hashCode, out handle))
            {
                result = handle.GetString(internable);
                if (result != null)
                {
                    // We have a hit, put the handle back in the cache.
                    if (!_stringsByHashCode.TryAdd(hashCode, handle))
                    {
                        // Another thread has managed to add a handle for the same hash code, so the one we got can be freed.
                        handle.Free();
                    }
                    cacheHit = true;
                    return result;
                }
            }
            else
            {
                handle = new StringWeakHandle();
                addingNewHandle = true;
            }

            // We don't have the string in the cache - create it.
            result = internable.ExpensiveConvertToString();

            // Set the handle to reference the new string and put it in the cache.
            handle.SetString(result);
            if (!_stringsByHashCode.TryAdd(hashCode, handle))
            {
                // Another thread has managed to add a handle for the same hash code, so the one we got can be freed.
                handle.Free();
            }

            // Remove unused handles if our heuristic indicates that it would be productive. Note that the _scavengeThreshold field
            // accesses are not protected from races. Being atomic (as guaranteed by the 32-bit data type) is enough here.
            if (addingNewHandle)
            {
                // Prevent the dictionary from growing forever with GC handles that don't reference live strings anymore.
                if (_stringsByHashCode.Count >= _scavengeThreshold)
                {
                    // Before we start scavenging set _scavengeThreshold to a high value to effectively lock other threads from
                    // running Scavenge at the same time (minus rare races).
                    _scavengeThreshold = int.MaxValue;
                    try
                    {
                        // Get rid of unused handles.
                        Scavenge();
                    }
                    finally
                    {
                        // And do this again when the number of handles reaches double the current after-scavenge number.
                        _scavengeThreshold = _stringsByHashCode.Count * 2;
                    }
                }
            }

            cacheHit = false;
            return result;
        }

        /// <summary>
        /// Iterates over the cache and removes unused GC handles, i.e. handles that don't reference live strings.
        /// This is expensive so try to call such that the cost is amortized to O(1) per GetOrCreateEntry() invocation.
        /// </summary>
        public void Scavenge()
        {
            foreach (KeyValuePair<int, StringWeakHandle> entry in _stringsByHashCode)
            {
                if (!entry.Value.IsUsed && _stringsByHashCode.TryRemove(entry.Key, out StringWeakHandle removedHandle))
                {
                    // Note that the removed handle may be different from the one we got from the enumerator so check again
                    // and try to put it back if it's still in use.
                    if (!removedHandle.IsUsed || !_stringsByHashCode.TryAdd(entry.Key, removedHandle))
                    {
                        removedHandle.Free();
                    }
                }
            }
        }

        /// <summary>
        /// Returns internal debug counters calculated based on the current state of the cache.
        /// </summary>
        public DebugInfo GetDebugInfo()
        {
            return GetDebugInfoImpl();
        }
    }
}
