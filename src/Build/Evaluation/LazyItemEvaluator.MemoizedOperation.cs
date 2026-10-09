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
        public LazyItemOperation Operation { get; }
        private Dictionary<ISet<string>, OrderedItemDataCollection> _cache;

        private bool _isReferenced;
#if DEBUG
        private int _applyCalls;
#endif

        public MemoizedOperation(LazyItemOperation operation)
        {
            Operation = operation;
        }

        public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
        {
#if DEBUG
            CheckInvariant();
#endif

            Operation.Apply(listBuilder, globsToIgnore);

            // cache results if somebody is referencing this operation
            if (_isReferenced)
            {
                AddItemsToCache(globsToIgnore, listBuilder.ToImmutable());
            }
#if DEBUG
            _applyCalls++;
            CheckInvariant();
#endif
        }

#if DEBUG
        private void CheckInvariant()
        {
            if (_isReferenced)
            {
                var cacheCount = _cache?.Count ?? 0;
                Debug.Assert(_applyCalls == cacheCount, "Apply should only be called once per globsToIgnore. Otherwise caching is not working");
            }
            else
            {
                // non referenced operations should not be cached
                // non referenced operations should have as many apply calls as the number of cache keys of the immediate dominator with _isReferenced == true
                Debug.Assert(_cache == null);
            }
        }
#endif

        public bool TryGetFromCache(ISet<string> globsToIgnore, out OrderedItemDataCollection items)
        {
            if (_cache != null)
            {
                return _cache.TryGetValue(globsToIgnore, out items);
            }

            items = null;
            return false;
        }

        /// <summary>
        /// Somebody is referencing this operation
        /// </summary>
        public void MarkAsReferenced()
        {
            _isReferenced = true;
        }

        private void AddItemsToCache(ImmutableHashSet<string> globsToIgnore, OrderedItemDataCollection items)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<ISet<string>, OrderedItemDataCollection>();
            }

            _cache[globsToIgnore] = items;
        }
    }
}
