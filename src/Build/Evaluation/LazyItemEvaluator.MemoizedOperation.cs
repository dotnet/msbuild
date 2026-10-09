// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
#if DEBUG
using System.Diagnostics;
#endif
using Microsoft.Build.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
    where P : class, IProperty, IEquatable<P>, IValued
    where I : class, IItem<M>, IMetadataTable
    where M : class, IMetadatum
    where D : class, IItemDefinition<M>
{
    private class MemoizedOperation
    {
        public ItemOperation Operation { get; }
        private Dictionary<ISet<string>, OrderedItemDataCollection> _cache;

        private bool _isNeeded;
#if DEBUG
        private int _applyCalls;
#endif

        public MemoizedOperation(ItemOperation operation)
        {
            Operation = operation;
        }

        public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
#if DEBUG
            CheckInvariant();
#endif

            Operation.Apply(listBuilder, laterRemoveGlobPatterns);

            // cache results if somebody is referencing this operation
            if (_isNeeded)
            {
                AddItemsToCache(laterRemoveGlobPatterns, listBuilder.ToImmutable());
            }
#if DEBUG
            _applyCalls++;
            CheckInvariant();
#endif
        }

#if DEBUG
        private void CheckInvariant()
        {
            if (_isNeeded)
            {
                var cacheCount = _cache?.Count ?? 0;
                Debug.Assert(_applyCalls == cacheCount, "Apply should only be called once per laterRemoveGlobPatterns. Otherwise caching is not working");
            }
            else
            {
                // non referenced operations should not be cached
                // non referenced operations should have as many apply calls as the number of cache keys of the immediate dominator with _isNeeded == true
                Debug.Assert(_cache == null);
            }
        }
#endif

        public bool TryGetFromCache(ISet<string> laterRemoveGlobPatterns, out OrderedItemDataCollection items)
        {
            if (_cache != null)
            {
                return _cache.TryGetValue(laterRemoveGlobPatterns, out items);
            }

            items = null;
            return false;
        }

        /// <summary>
        /// Somebody is referencing this operation
        /// </summary>
        public void MarkAsNeeded()
        {
            _isNeeded = true;
        }

        private void AddItemsToCache(ImmutableHashSet<string> laterRemoveGlobPatterns, OrderedItemDataCollection items)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<ISet<string>, OrderedItemDataCollection>();
            }

            _cache[laterRemoveGlobPatterns] = items;
        }
    }
}
