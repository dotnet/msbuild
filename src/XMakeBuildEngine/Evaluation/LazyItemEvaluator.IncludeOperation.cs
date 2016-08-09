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
        class IncludeOperation : LazyItemOperation
        {
            readonly int _elementOrder;
            
            readonly string _rootDirectory;
            
            readonly bool _conditionResult;

            readonly ImmutableList<string> _excludes;

            readonly ImmutableList<ProjectMetadataElement> _metadata;

            readonly IItemFactory<I, I> _itemFactory;

            public IncludeOperation(IncludeOperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _elementOrder = builder.ElementOrder;
                _rootDirectory = builder.RootDirectory;
                
                _conditionResult = builder.ConditionResult;

                _excludes = builder.Excludes.ToImmutable();
                if (builder.Metadata != null)
                {
                    _metadata = builder.Metadata.ToImmutable();
                }


                _itemFactory = new ItemFactoryWrapper(_itemElement, _lazyEvaluator._itemFactory);
            }

            protected override ICollection<I> SelectItems(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                List<I> itemsToAdd = new List<I>();

                Lazy<Func<string, bool>> excludeTester = null;
                ImmutableList<string>.Builder excludePatterns = ImmutableList.CreateBuilder<string>();
                if (_excludes != null)
                {
                    // STEP 4: Evaluate, split, expand and subtract any Exclude
                    foreach (string exclude in _excludes)
                    {
                        string excludeExpanded = _expander.ExpandIntoStringLeaveEscaped(exclude, ExpanderOptions.ExpandPropertiesAndItems, _itemElement.ExcludeLocation);
                        IList<string> excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded);
                        excludePatterns.AddRange(excludeSplits);
                    }

                    if (excludePatterns.Any())
                    {
                        excludeTester = new Lazy<Func<string, bool>>(() => EngineFileUtilities.GetMatchTester(excludePatterns));
                    }
                }

                foreach (var operation in _operations)
                {
                    if (operation.Item1 == ItemOperationType.Expression)
                    {
                        // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                        bool throwaway;
                        var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                            (ExpressionShredder.ItemExpressionCapture) operation.Item2, _evaluatorData, _itemFactory, ExpanderOptions.ExpandItems,
                            false /* do not include null expansion results */, out throwaway, _itemElement.IncludeLocation);

                        if (excludeTester != null)
                        {
                            itemsToAdd.AddRange(itemsFromExpression.Where(item => !excludeTester.Value(item.EvaluatedInclude)));
                        }
                        else
                        {
                            itemsToAdd.AddRange(itemsFromExpression);
                        }
                    }
                    else if (operation.Item1 == ItemOperationType.Value)
                    {
                        string value = (string)operation.Item2;

                        if (excludeTester == null ||
                            !excludeTester.Value(value))
                        {
                            var item = _itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath);
                            itemsToAdd.Add(item);
                        }
                    }
                    else if (operation.Item1 == ItemOperationType.Glob)
                    {
                        string glob = (string)operation.Item2;
                        string[] includeSplitFilesEscaped = EngineFileUtilities.GetFileListEscaped(_rootDirectory, glob,
                            excludePatterns.Count > 0 ? (IEnumerable<string>) excludePatterns.Concat(globsToIgnore) : globsToIgnore);
                        foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                        {
                            itemsToAdd.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(operation.Item1.ToString());
                    }
                }

                return itemsToAdd;
            }

            protected override void MutateItems(ICollection<I> items)
            {
                if (_metadata.Any())
                {
                    ////////////////////////////////////////////////////
                    // UNDONE: Implement batching here.
                    //
                    // We want to allow built-in metadata in metadata values here. 
                    // For example, so that an Idl file can specify that its Tlb output should be named %(Filename).tlb.
                    // 
                    // In other words, we want batching. However, we won't need to go to the trouble of using the regular batching code!
                    // That's because that code is all about grouping into buckets of similar items. In this context, we're not
                    // invoking a task, and it's fine to process each item individually, which will always give the correct results.
                    //
                    // For the CTP, to make the minimal change, we will not do this quite correctly.
                    //
                    // We will do this:
                    // -- check whether any metadata values or their conditions contain any bare built-in metadata expressions,
                    //    or whether they contain any custom metadata && the Include involved an @(itemlist) expression.
                    // -- if either case is found, we go ahead and evaluate all the metadata separately for each item.
                    // -- otherwise we can do the old thing (evaluating all metadata once then applying to all items)
                    // 
                    // This algorithm gives the correct results except when:
                    // -- batchable expressions exist on the include, exclude, or condition on the item element itself
                    //
                    // It means that 99% of cases still go through the old code, which is best for the CTP.
                    // When we ultimately implement this correctly, we should make sure we optimize for the case of very many items
                    // and little metadata, none of which varies between items.
                    List<string> values = new List<string>(_metadata.Count * 2);

                    foreach (ProjectMetadataElement metadatumElement in _metadata)
                    {
                        values.Add(metadatumElement.Value);
                        values.Add(metadatumElement.Condition);
                    }

                    ItemsAndMetadataPair itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(values);

                    bool needToProcessItemsIndividually = false;

                    if (itemsAndMetadataFound.Metadata != null && itemsAndMetadataFound.Metadata.Values.Count > 0)
                    {
                        // If there is bare metadata of any kind, and the Include involved an item list, we should
                        // run items individually, as even non-built-in metadata might differ between items

                        if (_referencedItemLists.Count >= 0)
                        {
                            needToProcessItemsIndividually = true;
                        }
                        else
                        {
                            // If there is bare built-in metadata, we must always run items individually, as that almost
                            // always differs between items.

                            // UNDONE: When batching is implemented for real, we need to make sure that
                            // item definition metadata is included in all metadata operations during evaluation
                            if (itemsAndMetadataFound.Metadata.Values.Count > 0)
                            {
                                needToProcessItemsIndividually = true;
                            }
                        }
                    }

                    if (needToProcessItemsIndividually)
                    {
                        foreach (I item in items)
                        {
                            _expander.Metadata = item;

                            foreach (ProjectMetadataElement metadatumElement in _metadata)
                            {
#if FEATURE_MSBUILD_DEBUGGER
                                //if (DebuggerManager.DebuggingEnabled)
                                //{
                                //    DebuggerManager.PulseState(metadatumElement.Location, _itemPassLocals);
                                //}
#endif

                                if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll, _expander, _lazyEvaluator))
                                {
                                    continue;
                                }

                                string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                                item.SetMetadata(metadatumElement, evaluatedValue);
                            }
                        }

                        // End of legal area for metadata expressions.
                        _expander.Metadata = null;
                    }

                    // End of pseudo batching
                    ////////////////////////////////////////////////////
                    // Start of old code
                    else
                    {
                        // Metadata expressions are allowed here.
                        // Temporarily gather and expand these in a table so they can reference other metadata elements above.
                        EvaluatorMetadataTable metadataTable = new EvaluatorMetadataTable(_itemType);
                        _expander.Metadata = metadataTable;

                        // Also keep a list of everything so we can get the predecessor objects correct.
                        List<Pair<ProjectMetadataElement, string>> metadataList = new List<Pair<ProjectMetadataElement, string>>();

                        foreach (ProjectMetadataElement metadatumElement in _metadata)
                        {
                            // Because of the checking above, it should be safe to expand metadata in conditions; the condition
                            // will be true for either all the items or none
                            if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll, _expander, _lazyEvaluator))
                            {
                                continue;
                            }

#if FEATURE_MSBUILD_DEBUGGER
                            //if (DebuggerManager.DebuggingEnabled)
                            //{
                            //    DebuggerManager.PulseState(metadatumElement.Location, _itemPassLocals);
                            //}
#endif

                            string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                            metadataTable.SetValue(metadatumElement, evaluatedValue);
                            metadataList.Add(new Pair<ProjectMetadataElement, string>(metadatumElement, evaluatedValue));
                        }

                        // Apply those metadata to each item
                        // Note that several items could share the same metadata objects

                        // Set all the items at once to make a potential copy-on-write optimization possible.
                        // This is valuable in the case where one item element evaluates to
                        // many items (either by semicolon or wildcards)
                        // and that item also has the same piece/s of metadata for each item.
                        _itemFactory.SetMetadata(metadataList, items);

                        // End of legal area for metadata expressions.
                        _expander.Metadata = null;
                    }
                }
            }

            protected override void SaveItems(ICollection<I> items, ImmutableList<ItemData>.Builder listBuilder)
            {
                listBuilder.AddRange(items.Select(item => new ItemData(item, _elementOrder, _conditionResult)));
            }
        }

        class IncludeOperationBuilder : OperationBuildWithMetadata
        {
            public int ElementOrder { get; set; }
            public string RootDirectory { get; set; }
            
            public bool ConditionResult { get; set; }
            
            public ImmutableList<string>.Builder Excludes { get; set; } = ImmutableList.CreateBuilder<string>();

            public IncludeOperation CreateOperation(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
            {
                return new IncludeOperation(this, lazyEvaluator);
            }
        }
    }
}
