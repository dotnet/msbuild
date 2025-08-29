﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.NET.StringTools
{
    /// <summary>
    /// Implements the WeakStringCache functionality on .NET Framework 3.5 where ConcurrentDictionary is not available.
    /// </summary>
    internal sealed partial class WeakStringCache : IDisposable
    {
        private readonly Dictionary<int, StringWeakHandle> _stringsByHashCode;

        public WeakStringCache()
        {
            _stringsByHashCode = new Dictionary<int, StringWeakHandle>(_initialCapacity);
        }

        /// <summary>
        /// Main entrypoint of this cache. Tries to look up a string that matches the given internable. If it succeeds, returns
        /// the string and sets cacheHit to true. If the string is not found, calls ExpensiveConvertToString on the internable,
        /// adds the resulting string to the cache, and returns it, setting cacheHit to false.
        /// </summary>
        /// <param name="internable">The internable describing the string we're looking for.</param>
        /// <param name="cacheHit">Whether the entry was already in the cache.</param>
        /// <returns>A string matching the given internable.</returns>
        public string GetOrCreateEntry(ref InternableString internable, out bool cacheHit)
        {
            int hashCode = internable.GetHashCode();

            StringWeakHandle handle;
            string? result;
            bool addingNewHandle = false;

            lock (_stringsByHashCode)
            {
                if (_stringsByHashCode.TryGetValue(hashCode, out handle))
                {
                    result = handle.GetString(ref internable);
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

                // Set the handle to reference the new string.
                handle.SetString(result);

                if (addingNewHandle)
                {
                    // Prevent the dictionary from growing forever with GC handles that don't reference live strings anymore.
                    if (_stringsByHashCode.Count >= _scavengeThreshold)
                    {
                        // Get rid of unused handles.
                        ScavengeNoLock();
                        // And do this again when the number of handles reaches double the current after-scavenge number.
                        _scavengeThreshold = _stringsByHashCode.Count * 2;
                    }
                }
                _stringsByHashCode[hashCode] = handle;
            }

            cacheHit = false;
            return result;
        }

        /// <summary>
        /// Iterates over the cache and removes unused GC handles, i.e. handles that don't reference live strings.
        /// This is expensive so try to call such that the cost is amortized to O(1) per GetOrCreateEntry() invocation.
        /// Assumes the lock is taken by the caller.
        /// </summary>
        private void ScavengeNoLock()
        {
            List<int>? keysToRemove = null;
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
        }

        /// <summary>
        /// Public version of ScavengeUnderLock() which takes the lock.
        /// </summary>
        public void Scavenge()
        {
            lock (_stringsByHashCode)
            {
                ScavengeNoLock();
            }
        }

        /// <summary>
        /// Returns internal debug counters calculated based on the current state of the cache.
        /// </summary>
        public DebugInfo GetDebugInfo()
        {
            lock (_stringsByHashCode)
            {
                return GetDebugInfoImpl();
            }
        }
    }
}
