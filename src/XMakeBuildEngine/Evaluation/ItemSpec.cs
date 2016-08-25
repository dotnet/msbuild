// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{

    /// <summary>
    /// Represents the elements of an item specification string and 
    /// provides some operations over them (like matching items against a given ItemSpec)
    /// </summary>
    internal class ItemSpec<P, I>
        where P : class, IProperty
        where I : class, IItem
    {
        public string ItemSpecString { get; }

        /// <summary>
        /// The fragments that compose an item spec string (values, globs, item references)
        /// </summary>
        public IEnumerable<ItemFragment> Fragments { get; }

        /// <summary>
        /// The expander needs to have a default item factory set.
        /// </summary>
        public Expander<P, I> Expander { get; set; }

        /// <summary>
        /// The xml attribute where this itemspec comes from
        /// </summary>
        public IElementLocation ItemSpecLocation { get; }

        /// <param name="itemSpec">The string containing item syntax</param>
        /// <param name="expander">Expects the expander to have a default item factory set</param>
        /// <param name="itemSpecLocation">The xml location the itemspec comes from</param>
        public ItemSpec(string itemSpec, Expander<P, I> expander, IElementLocation itemSpecLocation)
        {
            ItemSpecString = itemSpec;
            Expander = expander;
            ItemSpecLocation = itemSpecLocation;

            Fragments = BuildItemFragments(itemSpecLocation);
        }

        private IEnumerable<ItemFragment> BuildItemFragments(IElementLocation itemSpecLocation)
        {
            var builder = ImmutableList.CreateBuilder<ItemFragment>();

            //  Code corresponds to Evaluator.CreateItemsFromInclude

            // STEP 1: Expand properties in Include
            var evaluatedIncludeEscaped = Expander.ExpandIntoStringLeaveEscaped(ItemSpecString, ExpanderOptions.ExpandProperties, itemSpecLocation);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedIncludeEscaped.Length > 0)
            {
                var includeSplitsEscaped =
                    ExpressionShredder.SplitSemiColonSeparatedList(evaluatedIncludeEscaped);

                foreach (var includeSplitEscaped in includeSplitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool isItemListExpression;
                    var itemReferenceFragment = ProcessItemExpression(includeSplitEscaped, itemSpecLocation, out isItemListExpression);

                    if (isItemListExpression)
                    {
                        builder.Add(itemReferenceFragment);
                    }
                    else
                    {
                        // The expression is not of the form "@(X)". Treat as string

                        //  Code corresponds to EngineFileUtilities.GetFileList
                        var containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(includeSplitEscaped);
                        var containsRealWildcards = FileMatcher.HasWildcards(includeSplitEscaped);

                        // '*' is an illegal character to have in a filename.
                        // todo file-system assumption on legal path characters: https://github.com/Microsoft/msbuild/issues/781
                        if (containsEscapedWildcards && containsRealWildcards)
                        {

                            // Just return the original string.
                            builder.Add(new ValueFragment(includeSplitEscaped));
                        }
                        else if (!containsEscapedWildcards && containsRealWildcards)
                        {
                            // Unescape before handing it to the filesystem.
                            var filespecUnescaped = EscapingUtilities.UnescapeAll(includeSplitEscaped);

                            builder.Add(new GlobFragment(filespecUnescaped));
                        }
                        else
                        {
                            // No real wildcards means we just return the original string.  Don't even bother 
                            // escaping ... it should already be escaped appropriately since it came directly
                            // from the project file

                            builder.Add(new ValueFragment(includeSplitEscaped));
                        }
                    }
                }
            }

            return builder.ToImmutable();
        }

        private ItemExpressionFragment<P, I> ProcessItemExpression(string expression, IElementLocation elementLocation, out bool isItemListExpression)
        {
            isItemListExpression = false;

            //  Code corresponds to Expander.ExpandSingleItemVectorExpressionIntoItems
            if (expression.Length == 0)
            {
                return null;
            }

            var capture = Expander<P, I>.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, ExpanderOptions.ExpandItems, elementLocation);

            if (capture == null)
            {
                return null;
            }

            isItemListExpression = true;

            return new ItemExpressionFragment<P, I>(capture, this);
        }

        public IEnumerable<I> FilterItems(IEnumerable<I> items)
        {
            return items.Where(i => Fragments.Any(f => f.MatchesItem(i.EvaluatedInclude)));
        }

        public IEnumerable<string> FilterItems(IEnumerable<string> items)
        {
            return items.Where(s => Fragments.Any(f => f.MatchesItem(s)));
        }
    }

    internal interface ItemFragment
    {
        bool MatchesItem(string itemToMatch);
    }

    internal class ValueFragment : ItemFragment
    {
        public string Value { get; }

        public ValueFragment(string value)
        {
            Value = value;
        }

        public bool MatchesItem(string itemToMatch)
        {
            // todo file-system assumption on case sensitivity https://github.com/Microsoft/msbuild/issues/781
            return Value.Equals(itemToMatch, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal class GlobFragment : ItemFragment
    {
        public string Glob { get; }

        private readonly Lazy<Func<string, bool>> _globMatcher;

        public GlobFragment(string glob)
        {
            Glob = glob;
            _globMatcher = new Lazy<Func<string, bool>>(() => EngineFileUtilities.GetMatchTester(Glob));
        }

        public bool MatchesItem(string itemToMatch)
        {
            return _globMatcher.Value(itemToMatch);
        }
    }

    internal class ItemExpressionFragment<P, I> : ItemFragment
        where P : class, IProperty
        where I : class, IItem
    {
        public ExpressionShredder.ItemExpressionCapture Capture { get; }
        private readonly ItemSpec<P, I> _containingItemSpec;
        private List<ValueFragment> _itemValueFragments;
        private Expander<P, I> _expander;

        public ItemExpressionFragment(ExpressionShredder.ItemExpressionCapture capture, ItemSpec<P, I> containingItemSpec)
        {
            Capture = capture;
            _containingItemSpec = containingItemSpec;
            _expander = _containingItemSpec.Expander;
        }

        public bool MatchesItem(string itemToMatch)
        {
            // cache referenced items as long as the expander does not change
            // reference equality works for now since the expander cannot mutate its item state (hopefully it stays that way)
            if (_itemValueFragments == null || _expander != _containingItemSpec.Expander)
            {
                _expander = _containingItemSpec.Expander;
                _itemValueFragments =_expander.ExpandExpressionCaptureIntoItems(Capture, false, _containingItemSpec.ItemSpecLocation)
                    .Select(i => new ValueFragment(i.EvaluatedInclude))
                    .ToList();
            }

            return _itemValueFragments.Any(v => v.MatchesItem(itemToMatch));
        }
    }
}