// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;

    internal class LinkedObjectsMap<KeyType> : IDisposable
    {
        private static object Lock { get; } = new object();
        private static UInt32 nextCollectionId = 0;
        private UInt32 nextLocalId = 0;

        // internal fore debugging
        internal object GetLockForDebug => Lock;

        internal IEnumerable<LinkedObject> GetActiveLinks()
        {
            lock (Lock)
            {
                foreach (var h in activeLinks.Values)
                {
                    if (h.IsValid && h.RemoterWeak.TryGetTarget(out var result))
                    {
                        yield return result;
                    }
                }
            }
        }

        private static Dictionary<UInt32, LinkedObjectsMap<KeyType>> collections = new Dictionary<UInt32, LinkedObjectsMap<KeyType>>();

        private Dictionary<UInt32, WeakHolder> activeLinks = new Dictionary<UInt32, WeakHolder>();
        private Dictionary<KeyType, WeakHolder> indexByKey = new Dictionary<KeyType, WeakHolder>();


        private static void Remove(UInt32 collectionId, UInt32 id)
        {
            if (id != 0)
            {
                lock (Lock)
                {
                    if (collections.TryGetValue(collectionId, out var collection))
                    {
                        if (collection.activeLinks.TryGetValue(id, out var holder))
                        {
                            collection.activeLinks.Remove(id);
                            if (holder.IsValid)
                            {
                                collection.indexByKey.Remove(holder.Key);
                            }
                        }
                    }
                }
            }
        }

        private bool TryGetUnderLock(KeyType key, out LinkedObject result)
        {
            if (!indexByKey.TryGetValue(key, out var holder))
            {
                result = null;
                return false;
            }

            if (!holder.IsValid)
            {
                result = null;
                return false;
            }

            if (holder.RemoterWeak.TryGetTarget(out result))
            {
                return true;
            }

            // Remove stale reference, it is Collected but Finalizer is not called yet.
            // clear the index
            indexByKey.Remove(key);

            // but keep id entry (so no other remoter can reclaim it until existing one is finalized.
            holder.Invalidate();
            return false;
        }

        private void AddUnderLock(LinkedObject ro, Action<UInt32> setter)
        {
            do
            {
                nextLocalId++;
            }
            while (nextLocalId == 0 || activeLinks.ContainsKey(nextLocalId));

            setter(nextLocalId);
            var holder = new WeakHolder(ro);

            activeLinks.Add(holder.LocalLinkId, holder);
            indexByKey.Add(holder.Key, holder);
        }

        private LinkedObject GetOrAdd(LinkedObject ro, Action<UInt32> setter)
        {
            lock (Lock)
            {
                if (this.CollectionId == 0)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (!TryGetUnderLock(ro.Key, out var result))
                {
                    result = ro;
                    AddUnderLock(ro, setter);
                }

                return result;
            }
        }

        private LinkedObjectsMap(UInt32 id)
        {
            this.CollectionId = id;
        }
        private UInt32 CollectionId { get; set; }

        public static LinkedObjectsMap<KeyType>  Create()
        {
            lock (Lock)
            {
                do
                {
                    nextCollectionId++;
                } while (nextCollectionId == 0 || collections.ContainsKey(nextCollectionId));
                var result = new LinkedObjectsMap<KeyType>(nextCollectionId);
                collections[nextCollectionId] = result;
                return result;
            }
        }

        public void GetActive<SourceType>(UInt32 localId, out SourceType active)
            where SourceType : class
        {
            if (localId == 0)
            {
                active = null;
                return;
            }

            lock (Lock)
            {
                if (!this.activeLinks.TryGetValue(localId, out var holder))
                {
                    throw new ObjectDisposedException(typeof(SourceType).Name);
                }

                active = (SourceType)holder.StrongReference;
            }
        }

        public bool GetOrCreate<LinkType, SourceType>(SourceType source, object context, out LinkType linked, bool slow = false)
            where SourceType : ISourceWithId
            where LinkType : LinkedObject<SourceType>, new()
        {
            if (source == null || source.IsNull)
            {
                linked = null;
                return false;
            }

            return GetOrCreate(source.Key, source, context, out linked, slow);
        }

        public bool GetOrCreate<LinkType, SourceType>(KeyType key, SourceType source, object context, out LinkType linked, bool slow = false) where LinkType : LinkedObject<SourceType>, new()
        {
            if (source == null)
            {
                linked = null;
                return false;
            }

            lock (Lock)
            {
                if (this.CollectionId == 0)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (TryGetUnderLock(key, out var r))
                {
                    linked = (LinkType)r;
                    return false;
                }
                else if (!slow)
                {
                    linked = new LinkType();
                    linked.Initialize(key, source, context);
                    linked.ActivateFast(this);
                    return true;
                }
            }

            linked = new LinkType();
            linked.Initialize(key, source, context);
            linked = (LinkType)linked.ActivateSlow(this);
            return true;
        }

        public void Dispose()
        {
            lock (Lock)
            {
                CollectionId = 0;
                activeLinks.Clear();
                indexByKey.Clear();
            }
        }

        public interface ISourceWithId
        {
            KeyType Key { get; }
            bool IsNull { get; }
        }

        public class LinkedObject<SourceType> : LinkedObject
        {
            public virtual void Initialize(KeyType key, SourceType source, object context)
            {
                this.Key = key;
                this.Source = source;
            }

            public override object StrongReference => this.Source;
            public SourceType Source { get; protected set; }
        }

        public class LinkedObject : ISourceWithId
        {
            public void ActivateFast(LinkedObjectsMap<KeyType> map)
            {
                this.CollectionId = map.CollectionId;
                map.AddUnderLock(this, (id) => this.LocalId = id);
            }

            public LinkedObject ActivateSlow(LinkedObjectsMap<KeyType> map)
            {
                this.CollectionId = map.CollectionId;
                return map.GetOrAdd(this, (id) => this.LocalId = id);
            }

            ~LinkedObject()
            {
                if (LocalId != 0)
                {
                    Remove(CollectionId, LocalId);
                }
            }

            private UInt32 CollectionId { get; set; }

            public UInt32 LocalId { get; private set; }

            public virtual object StrongReference => null;

            public KeyType Key { get; protected set; }

            public virtual bool IsNull => false;
        }

        private class WeakHolder
        {
            public WeakHolder(LinkedObject ro)
            {
                this.StrongReference = ro.StrongReference;
                this.LocalLinkId = ro.LocalId;
                this.Key = ro.Key;
                this.RemoterWeak = new WeakReference<LinkedObject>(ro);
            }

            public object StrongReference { get; private set; }

            public UInt32 LocalLinkId { get; private set; }

            public KeyType Key { get; private set; }

            public WeakReference<LinkedObject> RemoterWeak { get; private set; }

            public bool IsValid => LocalLinkId != 0;

            public void Invalidate()
            {
                this.StrongReference = null;
                this.LocalLinkId = 0;
                this.Key = default(KeyType);
                this.RemoterWeak = null;
            }
        }
    }
}
