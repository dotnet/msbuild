// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel
{
    internal class SnapshottingDictionary<TKey, TValue> :
        SnapshottingCollection<KeyValuePair<TKey, TValue>, IDictionary<TKey, TValue>>, IDictionary<TKey, TValue>
    {
        public SnapshottingDictionary()
            : base(new Dictionary<TKey, TValue>())
        {
        }

        public ICollection<TKey> Keys => GetSnapshot().Keys;

        public ICollection<TValue> Values => GetSnapshot().Values;

        public TValue this[TKey key]
        {
            get => GetSnapshot()[key];

            set
            {
                lock (Collection)
                {
                    Collection[key] = value;
                    snapshot = null;
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (Collection)
            {
                Collection.Add(key, value);
                snapshot = null;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return GetSnapshot().ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            lock (Collection)
            {
                bool removed = Collection.Remove(key);
                if (removed)
                {
                    snapshot = null;
                }

                return removed;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return GetSnapshot().TryGetValue(key, out value);
        }

        protected sealed override IDictionary<TKey, TValue> CreateSnapshot(IDictionary<TKey, TValue> collection)
        {
            return new Dictionary<TKey, TValue>(collection);
        }
    }
}
