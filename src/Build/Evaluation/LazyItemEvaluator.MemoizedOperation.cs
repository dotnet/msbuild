// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        private bool _isReferenced;

        public MemoizedOperation(LazyItemOperation operation)
        {
            Operation = operation;
        }

        /// <summary>
        ///  Applies the operation and admits its prefix result to the shared cache when referenced.
        /// </summary>
        /// <param name="lazyEvaluator">The evaluator that owns the cached prefix results.</param>
        /// <param name="listBuilder">The items produced by preceding operations.</param>
        /// <param name="globsToIgnore">The exclusion context used for this computation.</param>
        public void Apply(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            OrderedItemDataCollection.Builder listBuilder,
            ImmutableHashSet<string> globsToIgnore)
        {
#if DEBUG
            Debug.Assert(!lazyEvaluator.TryGetCachedItems(Operation, globsToIgnore, out _),
                "Apply should only be called for an uncached operation and exclusion context");
#endif

            Operation.Apply(listBuilder, globsToIgnore);

            // cache results if somebody is referencing this operation
            if (_isReferenced)
            {
                lazyEvaluator.CacheItems(Operation, globsToIgnore, listBuilder.ToImmutable());
            }
#if DEBUG
            Debug.Assert(lazyEvaluator.TryGetCachedItems(Operation, globsToIgnore, out _) == _isReferenced,
                "Only referenced operation results should be cached");
#endif
        }

        /// <summary>
        ///  Looks up this operation's prefix result in the evaluator's shared cache.
        /// </summary>
        /// <param name="lazyEvaluator">The evaluator that owns the cached prefix results.</param>
        /// <param name="globsToIgnore">The exclusion context required by the caller.</param>
        /// <param name="items">The cached result when a matching entry exists.</param>
        /// <returns>
        ///  Whether a result exists for this operation and exclusion context.
        /// </returns>
        public bool TryGetFromCache(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ImmutableHashSet<string> globsToIgnore,
            out OrderedItemDataCollection items)
            => lazyEvaluator.TryGetCachedItems(Operation, globsToIgnore, out items);

        /// <summary>
        /// Somebody is referencing this operation
        /// </summary>
        public void MarkAsReferenced()
        {
            _isReferenced = true;
        }
    }
}
