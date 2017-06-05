// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class RemoveOperation : LazyItemOperation
        {
            public RemoveOperation(OperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
            }

            protected override ICollection<I> SelectItems(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                return SelectItemsMatchingItemSpec(listBuilder, _itemElement.RemoveLocation).ToImmutableHashSet();
            }

            protected override void SaveItems(ICollection<I> items, ImmutableList<ItemData>.Builder listBuilder)
            {
                if (!_conditionResult)
                {
                    return;
                }

                // bug in ImmutableList<T>.Builder.RemoveAll. In some cases it will remove elements for which the RemoveAll predicate is false
                // MSBuild issue: https://github.com/Microsoft/msbuild/issues/2069
                // corefx issue: https://github.com/dotnet/corefx/issues/20609
                //listBuilder.RemoveAll(itemData => items.Contains(itemData.Item));

                // Replacing RemoveAll with Remove fixes the above issue 

                // DeLINQified for perf
                //var itemDataToRemove = listBuilder.Where(itemData => items.Contains(itemData.Item)).ToList();
                var itemDataToRemove = new List<ItemData>();
                foreach (var itemData in listBuilder)
                {
                    if (items.Contains(itemData.Item))
                    {
                        itemDataToRemove.Add(itemData);
                    }
                }

                foreach (var itemToRemove in itemDataToRemove)
                {
                    listBuilder.Remove(itemToRemove);
                }
            }

            public ImmutableHashSet<string>.Builder GetRemovedGlobs()
            {
                var builder = ImmutableHashSet.CreateBuilder<string>();

                if (!_conditionResult)
                {
                    return builder;
                }

                var globs = _itemSpec.Fragments.OfType<GlobFragment>().Select(g => g.ItemSpecFragment);

                builder.UnionWith(globs);

                return builder;
            }
        }
    }
}
