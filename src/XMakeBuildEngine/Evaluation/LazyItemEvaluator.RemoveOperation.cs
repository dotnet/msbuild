using System.Collections.Generic;
using System.Collections.Immutable;

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
                return SelectItemsMatchingItemSpec(listBuilder, _itemElement.RemoveLocation);
            }

            protected override void SaveItems(ICollection<I> items, ImmutableList<ItemData>.Builder listBuilder)
            {
                listBuilder.RemoveAll(itemData => items.Contains(itemData.Item));
            }

            public ImmutableHashSet<string>.Builder GetRemovedGlobs()
            {
                var ret = ImmutableHashSet.CreateBuilder<string>();
                foreach (var operation in _operations)
                {
                    if (operation.Item1 == ItemOperationType.Glob)
                    {
                        ret.Add((string)operation.Item2);
                    }
                }

                return ret;
            }
        }
    }
}
