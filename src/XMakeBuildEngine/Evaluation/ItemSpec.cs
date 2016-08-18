// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Expands item/property/metadata in expressions.</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    internal class ItemSpec
    {
        public string ItemSpecString { get; private set; }

        public IEnumerable<ItemFragment> Fragments { get; private set; }

        public ItemSpec(string itemSpec, ICollection<ItemFragment> itemFragments)
        {
            ItemSpecString = itemSpec;

            Fragments = itemFragments;
        }

        public static ItemSpec BuildItemSpec<P, I>(string itemSpec, Expander<P, I> expander, IElementLocation itemSpecLocation)
            where P : class, IProperty
            where I : class, IItem
        {
            var itemFragments = BuildItemFragments(itemSpec, expander, itemSpecLocation);

            return new ItemSpec(itemSpec, itemFragments);
        }

        private static ImmutableList<ItemFragment> BuildItemFragments<P, I>(string itemSpec, Expander<P, I> expander, IElementLocation itemSpecLocation)
            where P : class, IProperty
            where I : class, IItem
        {
            var builder = ImmutableList.CreateBuilder<ItemFragment>();

            //  Code corresponds to Evaluator.CreateItemsFromInclude

            // STEP 1: Expand properties in Include
            string evaluatedIncludeEscaped = expander.ExpandIntoStringLeaveEscaped(itemSpec, ExpanderOptions.ExpandProperties, itemSpecLocation);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedIncludeEscaped.Length > 0)
            {
                IList<string> includeSplitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedIncludeEscaped);

                foreach (string includeSplitEscaped in includeSplitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool isItemListExpression;
                    var itemReferenceFragment = ProcessSingleItemVectorExpression<P, I>(includeSplitEscaped, itemSpecLocation, out isItemListExpression);

                    if (isItemListExpression)
                    {
                        builder.Add(itemReferenceFragment);
                    }
                    else
                    {
                        // The expression is not of the form "@(X)". Treat as string

                        //  Code corresponds to EngineFileUtilities.GetFileList
                        bool containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(includeSplitEscaped);
                        bool containsRealWildcards = FileMatcher.HasWildcards(includeSplitEscaped);

                        if (containsEscapedWildcards && containsRealWildcards)
                        {
                            // Umm, this makes no sense.  The item's Include has both escaped wildcards and 
                            // real wildcards.  What does he want us to do?  Go to the file system and find
                            // files that literally have '*' in their filename?  Well, that's not going to 
                            // happen because '*' is an illegal character to have in a filename.

                            // Just return the original string.
                            builder.Add(new ValueFragment(includeSplitEscaped));
                        }
                        else if (!containsEscapedWildcards && containsRealWildcards)
                        {
                            // Unescape before handing it to the filesystem.
                            string filespecUnescaped = EscapingUtilities.UnescapeAll(includeSplitEscaped);

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

        private static ItemExpressionFragment ProcessSingleItemVectorExpression<P, I>(string expression, IElementLocation elementLocation, out bool isItemListExpression)
            where P : class, IProperty
            where I : class, IItem
        {
            isItemListExpression = false;

            //  Code corresponds to Expander.ExpandSingleItemVectorExpressionIntoItems
            if (expression.Length == 0)
            {
                return null;
            }

            ExpressionShredder.ItemExpressionCapture match = Expander<P, I>.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, ExpanderOptions.ExpandItems, elementLocation);

            if (match == null)
            {
                return null;
            }

            isItemListExpression = true;

            return new ItemExpressionFragment(match);
        }
    }

    internal abstract class ItemFragment
    {
    }

    internal class ValueFragment : ItemFragment
    {
        public string Value { get; private set; }

        public ValueFragment(string value)
        {
            Value = value;
        }
    }

    internal class GlobFragment : ItemFragment
    {
        public string Glob { get; private set; }

        public GlobFragment(string glob)
        {
            Glob = glob;
        }
    }

    internal class ItemExpressionFragment : ItemFragment
    {
        public ExpressionShredder.ItemExpressionCapture Capture { get; private set; }

        public ItemExpressionFragment(ExpressionShredder.ItemExpressionCapture capture)
        {
            Capture = capture;
        }
    }
}