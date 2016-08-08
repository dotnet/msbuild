using Microsoft.Build.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using System.Collections.Immutable;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        abstract class LazyItemOperation
        {
            protected readonly ProjectItemElement _itemElement;
            protected readonly string _itemType;

            //  If Item1 of tuplee is ItemOperationType.Expression, then Item2 is an ExpressionShredder.ItemExpressionCapture
            //  Otherwise, Item2 is a string (representing either the value or the glob)
            protected readonly ImmutableList<Tuple<ItemOperationType, object>> _operations;

            protected readonly ImmutableDictionary<string, LazyItemList> _referencedItemLists;

            protected readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;
            protected readonly EvaluatorData _evaluatorData;
            protected readonly Expander<P, I> _expander;


            public LazyItemOperation(OperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
            {
                _itemElement = builder.ItemElement;
                _itemType = builder.ItemType;
                _operations = builder.Operations.ToImmutable();
                _referencedItemLists = builder.ReferencedItemLists.ToImmutable();

                _lazyEvaluator = lazyEvaluator;
                _evaluatorData = new EvaluatorData(_lazyEvaluator._outerEvaluatorData, itemType => GetReferencedItems(itemType, ImmutableHashSet<string>.Empty));
                _expander = new Expander<P, I>(_evaluatorData, _evaluatorData);
            }

            IList<I> GetReferencedItems(string itemType, ImmutableHashSet<string> globsToIgnore)
            {
                LazyItemList itemList;
                if (_referencedItemLists.TryGetValue(itemType, out itemList))
                {
                    return itemList.GetItems(globsToIgnore)
                        .Where(ItemData => ItemData.ConditionResult)
                        .Select(itemData => itemData.Item)
                        .ToList();
                }
                else
                {
                    return ImmutableList<I>.Empty;
                }
            }

            public abstract void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore);


        }
    }
}
