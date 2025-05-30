// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        private class RemoveOperation : LazyItemOperation
        {
            private readonly ImmutableList<string> _matchOnMetadata;
            private MetadataTrie<P, I> _metadataSet;

            public RemoveOperation(RemoveOperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _matchOnMetadata = builder.MatchOnMetadata.ToImmutable();

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    _matchOnMetadata.IsEmpty || _itemSpec.Fragments.All(f => f is ItemSpec<P, I>.ItemExpressionFragment),
                    new BuildEventFileInfo(string.Empty),
                    "OM_MatchOnMetadataIsRestrictedToReferencedItems");

                if (!_matchOnMetadata.IsEmpty)
                {
                    _metadataSet = new MetadataTrie<P, I>(builder.MatchOnMetadataOptions, _matchOnMetadata, _itemSpec);
                }
            }

            /// <summary>
            /// Apply the Remove operation.
            /// </summary>
            /// <remarks>
            /// This override exists to apply the removing-everything short-circuit and to avoid creating a redundant list of items to remove.
            /// </remarks>
            protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                bool matchingOnMetadata = !_matchOnMetadata.IsEmpty;
                if (!matchingOnMetadata)
                {
                    if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
                    {
                        // Perf optimization: If the Remove operation references itself (e.g. <I Remove="@(I)"/>)
                        // then all items are removed and matching is not necessary
                        listBuilder.Clear();
                        return;
                    }

                    if (listBuilder.Count >= Traits.Instance.DictionaryBasedItemRemoveThreshold)
                    {
                        // Perf optimization: If the number of items in the running list is large, construct a dictionary,
                        // enumerate all items referenced by the item spec, and perform dictionary look-ups to find items
                        // to remove.
                        IList<string> matches = _itemSpec.IntersectsWith(listBuilder.Dictionary);
                        listBuilder.RemoveAll(matches);
                        return;
                    }
                }

                // todo Perf: do not match against the globs: https://github.com/dotnet/msbuild/issues/2329
                HashSet<I> items = null;
                foreach (ItemData item in listBuilder)
                {
                    bool isMatch = matchingOnMetadata ? MatchesItemOnMetadata(item.Item) : _itemSpec.MatchesItem(item.Item);
                    if (isMatch)
                    {
                        items ??= new HashSet<I>();
                        items.Add(item.Item);
                    }
                }
                if (items is not null)
                {
                    listBuilder.RemoveAll(items);
                }
            }

            private bool MatchesItemOnMetadata(I item)
            {
                return _metadataSet.Contains(_matchOnMetadata.Select(m => item.GetMetadataValue(m)));
            }

            public ImmutableHashSet<string>.Builder GetRemovedGlobs()
            {
                var builder = ImmutableHashSet.CreateBuilder<string>();

                if (!_conditionResult)
                {
                    return builder;
                }

                var globs = _itemSpec.Fragments.OfType<GlobFragment>().Select(g => g.TextFragment);

                builder.UnionWith(globs);

                return builder;
            }
        }

        private class RemoveOperationBuilder : OperationBuilder
        {
            public ImmutableList<string>.Builder MatchOnMetadata { get; } = ImmutableList.CreateBuilder<string>();

            public MatchOnMetadataOptions MatchOnMetadataOptions { get; set; }

            public RemoveOperationBuilder(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
            {
            }
        }
    }
}
