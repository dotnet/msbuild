// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed class RemoveOperation : ItemOperation
    {
        private readonly ItemSpec _itemSpec;

        private ItemSpecMatcher<P, I> Matcher => field ??= new(_itemSpec, _expander);

        public RemoveOperation(
            LazyItemEvaluator<P, I, M, D> lazyEvaluator,
            ProjectItemElement itemElement,
            ItemSpec itemSpec,
            bool conditionResult,
            ItemListBookmarks itemListBookmarks)
            : base(lazyEvaluator, itemElement, conditionResult, itemListBookmarks)
        {
            _itemSpec = itemSpec;
        }

        /// <summary>
        /// Apply the Remove operation.
        /// </summary>
        /// <remarks>
        /// This override exists to apply the removing-everything short-circuit and to avoid creating a redundant list of items to remove.
        /// </remarks>
        protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> laterRemoveGlobPatterns)
        {
            if (!_conditionResult)
            {
                return;
            }

            if (_itemSpec.IsSimpleItemVector(_itemElement.ItemType))
            {
                // Perf optimization: If the Remove operation references itself (e.g. <I Remove="@(I)"/>)
                // then all items are removed and matching is not necessary
                listBuilder.Clear();
                return;
            }

            if (listBuilder.Count >= Traits.Instance.DictionaryBasedItemRemoveThreshold)
            {
                // For large item lists, index normalized paths and use membership lookups to find items to remove.
                ISet<NormalizedItemPath> matches = Matcher.IntersectsWith(listBuilder.NormalizedItemPaths);
                listBuilder.RemoveAll(matches);
                return;
            }

            // todo Perf: do not match against the globs: https://github.com/dotnet/msbuild/issues/2329
            HashSet<I>? items = null;
            foreach (ItemData item in listBuilder)
            {
                if (Matcher.MatchesItem(item.Item))
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

        public ImmutableHashSet<string>.Builder GetRemoveGlobPatterns()
        {
            var builder = s_emptyRemoveGlobPatterns.ToBuilder();

            if (!_conditionResult)
            {
                return builder;
            }

            var globs = _itemSpec.Fragments.OfType<GlobFragment>().Select(g => g.Text);

            builder.UnionWith(globs);

            return builder;
        }
    }
}
