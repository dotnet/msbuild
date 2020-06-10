
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
        /// This method performs one operation on the underlying ConcurrentDictionary on cache hit, and two or three operations on cache miss.
        /// 1. It checks whether the dictionary has a matching entry. This operations is common to all code paths.
        ///    If there is a matching entry we are done.
        /// 2. If the dictionary doesn't have an entry for the given hash code, we make a new one and add it (the second operation).
        ///    Note that we could do 1. and 2. together using GetOrAdd() with the valueFactory callback but it wouldn't be much faster
        ///    and would require allocating a closure object to share data with the callback.
        /// 3. If the dictionary has an entry for the given hash code but it doesn't match the argument because it's either already
        ///    collected or there is a hash collision, we have to first remove the existing handle to prevent other threads from
        ///    freeing it (second operation). Only then can it have the target set to the new string and be added back to the dictionary
        ///    (third operation).
        /// </remarks>
        public string GetOrCreateEntry<T>(T internable, out bool cacheHit) where T : IInternable
        {
            int hashCode = GetInternableHashCode(internable);

            StringWeakHandle handle;
            string result;
            bool addingNewHandle = false;

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
                addingNewHandle = true;
            }

            // We don't have the string in the cache - create it.
            result = internable.ExpensiveConvertToString();

            // If the handle is new, we have to add it to the cache. We do it after removing unused handles if our heuristic
            // indicates that it would be productive. Note that the _capacity field accesses are not protected from races. Being
            // atomic (as guaranteed by the 32-bit data type) is enough here.
            if (addingNewHandle)
            {
                // Prevent the dictionary from growing forever with GC handles that don't reference live strings anymore.
                if (_stringsByHashCode.Count >= _scavengeThreshold)
                {
                    // Get rid of unused handles.
                    Scavenge();
                    // And do this again when the number of handles reaches double the current after-scavenge number.
                    _scavengeThreshold = _stringsByHashCode.Count * 2;
                }
            }
            else
            {
                // If the handle is already in the cache, we have to be careful because other threads may be operating on it.
                // In particular the Scavenge method may free the handle from underneath us if we leave it in the cache.
                if (!_stringsByHashCode.TryRemove(hashCode, out handle))
                {
                    // The handle is no longer in the cache so we're creating a new one after all.
                    handle = new StringWeakHandle();
                }
            }

            // Set the handle to reference the new string and put it in the cache.
            handle.SetString(result);
            if (!_stringsByHashCode.TryAdd(hashCode, handle))
            {
                // If somebody beat us to it and the new handle has not been added, free it.
                handle.Free();
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
