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
        enum RemoveOperationType
        {
            Value,
            Glob,
            Expression
        }

        class RemoveOperation : LazyItemOperation
        {

            //  This is used only when evaluating an expression, which instantiates
            //  the items and then removes them. 
            readonly IItemFactory<I, I> _itemFactory;

            public RemoveOperation(OperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _itemFactory = new ItemFactoryWrapper(_itemElement, _lazyEvaluator._itemFactory);
            }

            public override void Apply(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                //  TODO: Figure out case sensitivity on non-Windows OS's
                HashSet<string> itemSpecsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<string> globsToRemove = new List<string>();


                foreach (var operation in _operations)
                {
                    if (operation.Item1 == ItemOperationType.Expression)
                    {
                        //  TODO: consider optimizing the case where an item element removes all items of its
                        //  item type, for example:
                        //      <Compile Remove="@(Compile)" />
                        //  In this case we could avoid evaluating previous versions of the list entirely
                        bool throwaway;
                        var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                            (ExpressionShredder.ItemExpressionCapture)operation.Item2, _evaluatorData, _itemFactory, ExpanderOptions.ExpandItems,
                            false /* do not include null expansion results */, out throwaway, _itemElement.RemoveLocation);

                        foreach (var item in itemsFromExpression)
                        {
                            itemSpecsToRemove.Add(item.EvaluatedInclude);
                        }
                    }
                    else if (operation.Item1 == ItemOperationType.Value)
                    {
                        itemSpecsToRemove.Add((string)operation.Item2);
                    }
                    else if (operation.Item1 == ItemOperationType.Glob)
                    {
                        string glob = (string)operation.Item2;
                        globsToRemove.Add(glob);
                    }
                    else
                    {
                        throw new InvalidOperationException(operation.Item1.ToString());
                    }
                }

                var removeMatcher = EngineFileUtilities.GetMatchTester(globsToRemove);

                listBuilder.RemoveAll(item => itemSpecsToRemove.Contains(item.Item.EvaluatedInclude) ||
                    removeMatcher(item.Item.EvaluatedInclude));
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
