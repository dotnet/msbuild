// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Expands bare metadata expressions, like %(Compile.WarningLevel), or unqualified, like %(Compile).
    /// </summary>
    /// <remarks>
    /// This is a private nested class, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private static class MetadataExpander
    {
        /// <summary>
        /// Expands all embedded item metadata in the given string, using the bucketed items.
        /// Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile).
        /// </summary>
        /// <param name="expression">The expression containing item metadata references.</param>
        /// <param name="metadata">The metadata to be expanded.</param>
        /// <param name="options">Used to specify what to expand.</param>
        /// <param name="elementLocation">The location information for error reporting purposes.</param>
        /// <param name="loggingContext">The logging context for this operation.</param>
        /// <returns>The string with item metadata expanded in-place, escaped.</returns>
        internal static string ExpandMetadataLeaveEscaped(string expression, IMetadataTable metadata, ExpanderOptions options, IElementLocation elementLocation, LoggingContext loggingContext = null)
        {
            try
            {
                if ((options & ExpanderOptions.ExpandMetadata) == 0)
                {
                    return expression;
                }

                Assumed.NotNull(metadata, "Cannot expand metadata without providing metadata");

                // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item metadata references, just bail
                // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!
                if (s_invariantCompareInfo.IndexOf(expression, "%(", CompareOptions.Ordinal) == -1)
                {
                    return expression;
                }

                string result = null;

                if (s_invariantCompareInfo.IndexOf(expression, "@(", CompareOptions.Ordinal) == -1)
                {
                    // if there are no item vectors in the string
                    // run a simpler Regex to find item metadata references
                    MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);

                    using SpanBasedStringBuilder finalResultBuilder = Strings.GetSpanBasedStringBuilder();
                    RegularExpressions.ReplaceAndAppend(expression, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.ItemMetadataRegex);

                    // Don't create more strings
                    if (finalResultBuilder.Equals(expression.AsSpan()))
                    {
                        // If the final result is the same as the original expression, then just return the original expression
                        result = expression;
                    }
                    else
                    {
                        // Otherwise, convert the final result to a string
                        // and return that.
                        result = finalResultBuilder.ToString();
                    }
                }
                else
                {
                    ExpressionShredder.ReferencedItemExpressionsEnumerator itemVectorExpressionsEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

                    // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                    using SpanBasedStringBuilder finalResultBuilder = Strings.GetSpanBasedStringBuilder();

                    int start = 0;

                    if (itemVectorExpressionsEnumerator.MoveNext())
                    {
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);
                        ExpressionShredder.ItemExpressionCapture firstItemExpressionCapture = itemVectorExpressionsEnumerator.Current;

                        if (itemVectorExpressionsEnumerator.MoveNext())
                        {
                            // we're in the uncommon case with a partially enumerated enumerator. We need to process the first two items we enumerated and the remaining ones.
                            // Move over the expression, skipping those that have been recognized as an item vector expression
                            // Anything other than an item vector expression we want to expand bare metadata in.
                            start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, firstItemExpressionCapture);
                            start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, itemVectorExpressionsEnumerator.Current);

                            while (itemVectorExpressionsEnumerator.MoveNext())
                            {
                                start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, itemVectorExpressionsEnumerator.Current);
                            }
                        }
                        else
                        {
                            // There is only one item. Check to see if we're in the common case.
                            if (firstItemExpressionCapture.Value == expression && firstItemExpressionCapture.Separator == null)
                            {
                                // The most common case is where the transform is the whole expression
                                // Also if there were no valid item vector expressions found, then go ahead and do the replacement on
                                // the whole expression (which is what Orcas did).
                                return expression;
                            }
                            else
                            {
                                start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, firstItemExpressionCapture);
                            }
                        }
                    }

                    // If there's anything left after the last item vector expression
                    // then we need to metadata replace and then append that
                    if (start < expression.Length)
                    {
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);
                        string subExpressionToReplaceIn = expression.Substring(start);

                        RegularExpressions.ReplaceAndAppend(subExpressionToReplaceIn, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);
                    }

                    if (finalResultBuilder.Equals(expression.AsSpan()))
                    {
                        // If the final result is the same as the original expression, then just return the original expression
                        result = expression;
                    }
                    else
                    {
                        // Otherwise, convert the final result to a string
                        // and return that.
                        result = finalResultBuilder.ToString();
                    }
                }

                return result;
            }
            catch (InvalidOperationException ex)
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression, ex.Message);
            }

            return null;

            static int ProcessItemExpressionCapture(string expression, SpanBasedStringBuilder finalResultBuilder, MetadataMatchEvaluator matchEvaluator, int start, ExpressionShredder.ItemExpressionCapture itemExpressionCapture)
            {
                // Extract the part of the expression that appears before the item vector expression
                // e.g. the ABC in ABC@(foo->'%(FullPath)')
                string subExpressionToReplaceIn = expression.Substring(start, itemExpressionCapture.Index - start);

                RegularExpressions.ReplaceAndAppend(subExpressionToReplaceIn, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);

                // Expand any metadata that appears in the item vector expression's separator
                if (itemExpressionCapture.Separator != null)
                {
                    RegularExpressions.ReplaceAndAppend(itemExpressionCapture.Value, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, -1, itemExpressionCapture.SeparatorStart, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);
                }
                else
                {
                    // Append the item vector expression as is
                    // e.g. the @(foo->'%(FullPath)') in ABC@(foo->'%(FullPath)')
                    finalResultBuilder.Append(itemExpressionCapture.Value);
                }

                // Move onto the next part of the expression that isn't an item vector expression
                start = (itemExpressionCapture.Index + itemExpressionCapture.Length);
                return start;
            }
        }
    }
}
