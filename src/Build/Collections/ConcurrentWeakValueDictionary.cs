// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A thread-safe dictionary whose values are held via <see cref="System.WeakReference{T}"/>
    /// so that they do not prevent garbage collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lookup and per-key writes are lock-free (backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>).
    /// Compound multi-key updates must be coordinated by the caller.
    /// </para>
    /// <para>
    /// Unlike <c>WeakValueDictionary</c>, this collection is safe to read concurrently with writes.
    /// Unlike <see cref="ConcurrentDictionary{TKey,TValue}"/> with <see cref="System.WeakReference{T}"/> values,
    /// this collection automatically prunes entries whose targets have been garbage collected
    /// using a doubling-watermark, single-flight scheme: a sweep runs when <see cref="Count"/>
    /// reaches the current watermark; if it frees nothing, the watermark doubles.
    /// </para>
    /// <para>
    /// Be sure that the keys are strongly held, or unpredictable behavior will ensue.
    /// </para>
    /// </remarks>
    /// <typeparam name="TKey">Type of key.</typeparam>
    /// <typeparam name="TValue">Type of value, without the <see cref="System.WeakReference{T}"/> wrapper.</typeparam>
    internal sealed class ConcurrentWeakValueDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TValue : class
    {
        /// <summary>
        /// Initial sweep watermark. Mirrors the default capacity of <c>WeakValueDictionary</c>.
        /// </summary>
        private const int InitialScavengeWatermark = 10;

        private readonly ConcurrentDictionary<TKey, WeakReference<TValue>> _dictionary;

        /// <summary>
        /// When <see cref="Count"/> reaches this value the next mutator triggers a sweep.
        /// If the sweep removes nothing, the watermark is doubled, so per-add sweep cost
        /// is amortized O(log N).
        /// </summary>
        private int _scavengeWatermark = InitialScavengeWatermark;

        /// <summary>
        /// Single-flight latch for <see cref="ScavengeIfNeeded"/>; 0 = idle, 1 = in flight.
        /// </summary>
        private int _scavengeInFlight;

        public ConcurrentWeakValueDictionary()
            : this(comparer: null)
        {
        }

        public ConcurrentWeakValueDictionary(IEqualityComparer<TKey> comparer)
        {
            // On .NET Framework, ConcurrentDictionary's IEqualityComparer-taking constructor
            // rejects null. Fall back to the parameterless constructor in that case.
            _dictionary = comparer is null
                ? new ConcurrentDictionary<TKey, WeakReference<TValue>>()
                : new ConcurrentDictionary<TKey, WeakReference<TValue>>(comparer);
        }

        /// <summary>
        /// Number of entries currently in the dictionary, including those whose value's target
        /// has already been garbage collected but has not yet been swept.
        /// </summary>
        /// <remarks>
        /// Returned without walking the entries — cheap, but may over-count compared to the
        /// number of values currently observable via <see cref="TryGetValue"/>.
        /// </remarks>
        public int Count => _dictionary.Count;

        /// <summary>
        /// Attempts to retrieve the live value for the given key.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the key is present and its value's target has not been garbage collected;
        /// otherwise <c>false</c>. If the entry is present but dead, it is opportunistically removed.
        /// </returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!_dictionary.TryGetValue(key, out WeakReference<TValue> wrapped))
            {
                value = null;
                return false;
            }

            if (wrapped.TryGetTarget(out value))
            {
                return true;
            }

            // Opportunistic removal: only delete if the slot still holds this exact dead reference,
            // so we never clobber a fresh entry that another thread just published for the same key.
            TryRemoveExact(key, wrapped);
            value = null;
            return false;
        }

        /// <summary>
        /// Sets the value for the given key, overwriting any existing entry.
        /// Triggers an amortized sweep of dead entries when the dictionary reaches the current watermark.
        /// </summary>
        public TValue this[TKey key]
        {
            set
            {
                _dictionary[key] = new WeakReference<TValue>(value);
                ScavengeIfNeeded();
            }
        }

        /// <summary>
        /// Removes the entry with the given key if present, regardless of whether its value is still live.
        /// </summary>
        public bool TryRemove(TKey key) => _dictionary.TryRemove(key, out _);

        /// <summary>
        /// Removes the entry with the given key only if its value's target is referentially equal
        /// to <paramref name="expectedValue"/>. Used to avoid clobbering a freshly-published entry
        /// for the same key from another thread.
        /// </summary>
        public bool TryRemove(TKey key, TValue expectedValue)
        {
            if (!_dictionary.TryGetValue(key, out WeakReference<TValue> wrapped))
            {
                return false;
            }

            if (wrapped.TryGetTarget(out TValue current) && !ReferenceEquals(current, expectedValue))
            {
                return false;
            }

            return TryRemoveExact(key, wrapped);
        }

        /// <summary>
        /// Removes all entries.
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
            Volatile.Write(ref _scavengeWatermark, InitialScavengeWatermark);
        }

        /// <summary>
        /// Walk every entry once and remove those whose value's target has been collected.
        /// Returns the number of entries removed. Intended for tests and explicit cleanup paths;
        /// regular use is automatic via <see cref="ScavengeIfNeeded"/>.
        /// </summary>
        public int Scavenge()
        {
            int removed = 0;
            foreach (KeyValuePair<TKey, WeakReference<TValue>> kvp in _dictionary)
            {
                if (!kvp.Value.TryGetTarget(out _) && TryRemoveExact(kvp.Key, kvp.Value))
                {
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Enumerates live entries. Dead weak references are skipped (but not removed — call
        /// <see cref="Scavenge"/> for that). The enumerator is snapshot-ish: entries added or
        /// removed during enumeration may or may not be observed, matching <see cref="ConcurrentDictionary{TKey,TValue}"/>.
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<TKey, WeakReference<TValue>> kvp in _dictionary)
            {
                if (kvp.Value.TryGetTarget(out TValue target))
                {
                    yield return new KeyValuePair<TKey, TValue>(kvp.Key, target);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// If the dictionary has grown to the current watermark, run a single-flight sweep of dead
        /// weak references. If the sweep frees nothing, double the watermark so per-add cost is
        /// amortized O(log N) — the same trade-off <c>Dictionary{K,V}</c> makes for resizes.
        /// </summary>
        private void ScavengeIfNeeded()
        {
            int watermark = Volatile.Read(ref _scavengeWatermark);
            if (_dictionary.Count < watermark)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _scavengeInFlight, 1, 0) != 0)
            {
                return;
            }

            try
            {
                int removed = Scavenge();
                if (removed == 0)
                {
                    // No dead entries — the dictionary really is this large. Raise the bar so we
                    // don't sweep on every subsequent add. Never shrinks.
                    Volatile.Write(ref _scavengeWatermark, _dictionary.Count * 2);
                }
            }
            finally
            {
                Volatile.Write(ref _scavengeInFlight, 0);
            }
        }

        /// <summary>
        /// Atomically remove the entry only if the value slot still holds the exact
        /// <see cref="WeakReference{T}"/> instance we observed.
        /// </summary>
        private bool TryRemoveExact(TKey key, WeakReference<TValue> expectedWrapper)
        {
            return ((ICollection<KeyValuePair<TKey, WeakReference<TValue>>>)_dictionary)
                .Remove(new KeyValuePair<TKey, WeakReference<TValue>>(key, expectedWrapper));
        }
    }
}
