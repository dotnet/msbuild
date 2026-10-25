// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Removes items whose metadata tuples match values stored when the operation is collected.
    /// </summary>
    private sealed class MetadataRemoveOperation : ItemOperation
    {
        private readonly ImmutableArray<string> _matchOnMetadata;
        private readonly MetadataTrie _metadataSet;

        public MetadataRemoveOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            ItemSpec<P, I> itemSpec,
            bool conditionResult,
            ItemListBookmarks itemListBookmarks,
            ImmutableArray<string> matchOnMetadata,
            MatchOnMetadataOptions matchOnMetadataOptions)
            : base(lazyEvaluator, itemElement, itemSpec, conditionResult, itemListBookmarks)
        {
            _matchOnMetadata = matchOnMetadata;

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                itemSpec.Fragments.All(f => f is ItemSpec<P, I>.ItemExpressionFragment),
                ElementLocation.Empty,
                "OM_MatchOnMetadataIsRestrictedToReferencedItems");

            _metadataSet = MetadataTrie.Create(matchOnMetadataOptions, _matchOnMetadata, itemSpec);
        }

        protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            if (!_conditionResult)
            {
                return;
            }

            HashSet<I>? items = null;
            foreach (ItemData item in listBuilder)
            {
                if (_metadataSet.Contains(item.Item, _matchOnMetadata))
                {
                    items ??= [];
                    items.Add(item.Item);
                }
            }

            if (items is not null)
            {
                listBuilder.RemoveAll(items);
            }
        }
    }
}
