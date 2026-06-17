// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;
using ItemSpecModifiers = Microsoft.Build.Framework.ItemSpecModifiers;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Expands item expressions, like @(Compile), possibly with transforms and/or separators.
    ///
    /// Item vectors are composed of a name, an optional transform, and an optional separator i.e.
    ///
    ///     @(&lt;name&gt;->'&lt;transform&gt;','&lt;separator&gt;')
    ///
    /// If a separator is not specified it defaults to a semi-colon. The transform expression is also optional, but if
    /// specified, it allows each item in the vector to have its item-spec converted to a different form. The transform
    /// expression can reference any custom metadata defined on the item, as well as the pre-defined item-spec modifiers.
    ///
    /// NOTE:
    /// 1) white space between &lt;name&gt;, &lt;transform&gt; and &lt;separator&gt; is ignored
    ///    i.e. @(&lt;name&gt;, '&lt;separator&gt;') is valid
    /// 2) the separator is not restricted to be a single character, it can be a string
    /// 3) the separator can be an empty string i.e. @(&lt;name&gt;,'')
    /// 4) specifying an empty transform is NOT the same as specifying no transform -- the former will reduce all item-specs
    ///    to empty strings
    ///
    /// if @(files) is a vector for the files a.txt and b.txt, then:
    ///
    ///     "my list: @(files)"                                 expands to string     "my list: a.txt;b.txt"
    ///
    ///     "my list: @(files,' ')"                             expands to string      "my list: a.txt b.txt"
    ///
    ///     "my list: @(files, '')"                             expands to string      "my list: a.txtb.txt"
    ///
    ///     "my list: @(files, '; ')"                           expands to string      "my list: a.txt; b.txt"
    ///
    ///     "my list: @(files->'%(Filename)')"                  expands to string      "my list: a;b"
    ///
    ///     "my list: @(files -> 'temp\%(Filename).xml', ' ')   expands to string      "my list: temp\a.xml temp\b.xml"
    ///
    ///     "my list: @(files->'')                              expands to string      "my list: ;".
    /// </summary>
    /// <remarks>
    /// This is a private nested class, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private static partial class ItemExpander
    {
        private static readonly FrozenDictionary<string, ItemTransformFunctions> s_intrinsicItemFunctions = new Dictionary<string, ItemTransformFunctions>(StringComparer.OrdinalIgnoreCase)
        {
            { "Count", ItemTransformFunctions.Count },
            { "Exists", ItemTransformFunctions.Exists },
            { "Combine", ItemTransformFunctions.Combine },
            { "GetPathsOfAllDirectoriesAbove", ItemTransformFunctions.GetPathsOfAllDirectoriesAbove },
            { "DirectoryName", ItemTransformFunctions.DirectoryName },
            { "Metadata", ItemTransformFunctions.Metadata },
            { "DistinctWithCase", ItemTransformFunctions.DistinctWithCase },
            { "Distinct", ItemTransformFunctions.Distinct },
            { "Reverse", ItemTransformFunctions.Reverse },
            { "ExpandQuotedExpressionFunction", ItemTransformFunctions.ExpandQuotedExpressionFunction },
            { "ExecuteStringFunction", ItemTransformFunctions.ExecuteStringFunction },
            { "ClearMetadata", ItemTransformFunctions.ClearMetadata },
            { "HasMetadata", ItemTransformFunctions.HasMetadata },
            { "WithMetadataValue", ItemTransformFunctions.WithMetadataValue },
            { "WithoutMetadataValue", ItemTransformFunctions.WithoutMetadataValue },
            { "AnyHaveMetadataValue", ItemTransformFunctions.AnyHaveMetadataValue },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Execute the list of transform functions.
        /// </summary>
        /// <remarks>
        /// Each captured transform function will be mapped to to a either static method on
        /// <see cref="IntrinsicItemFunctions{S}"/> or a known item spec modifier which operates on the item path.
        ///
        /// For each function, the full list of items will be iteratvely tranformed using the output of the previous.
        ///
        /// E.g. given functions f, g, h, the order of operations will look like:
        /// results = h(g(f(items)))
        ///
        /// If no function name is found, we default to <see cref="IntrinsicItemFunctions{S}.ExpandQuotedExpressionFunction"/>.
        /// </remarks>
        /// <typeparam name="S">class, IItem.</typeparam>
        internal static List<KeyValuePair<string, S>> Transform<S>(
            Expander<P, I> expander,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            List<ExpressionShredder.ItemExpressionCapture> captures,
            ICollection<S> itemsOfType,
            out bool brokeEarly)
            where S : class, IItem
        {
            // Each transform runs on the full set of transformed items from the previous result.
            // We can reuse our buffers by just swapping the references after each transform.
            List<KeyValuePair<string, S>> sourceItems = IntrinsicItemFunctions<S>.GetItemPairs(itemsOfType);
            List<KeyValuePair<string, S>> transformedItems = new(itemsOfType.Count);

            // Create a TransformFunction for each transform in the chain by extracting the relevant information
            // from the regex parsing results
            for (int i = 0; i < captures.Count; i++)
            {
                ExpressionShredder.ItemExpressionCapture capture = captures[i];
                string function = capture.Value;
                string functionName = capture.FunctionName;
                string argumentsExpression = capture.FunctionArguments;

                string[] arguments = null;

                if (functionName == null)
                {
                    functionName = "ExpandQuotedExpressionFunction";
                    arguments = [function];
                }
                else if (argumentsExpression != null)
                {
                    arguments = ExtractFunctionArguments(elementLocation, argumentsExpression, argumentsExpression.AsMemory());
                }

                ItemTransformFunctions functionType;

                if (ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
                {
                    functionType = ItemTransformFunctions.ItemSpecModifierFunction;
                }
                else if (!s_intrinsicItemFunctions.TryGetValue(functionName, out functionType))
                {
                    functionType = ItemTransformFunctions.ExecuteStringFunction;
                }

                switch (functionType)
                {
                    case ItemTransformFunctions.ItemSpecModifierFunction:
                        IntrinsicItemFunctions<S>.ItemSpecModifierFunction(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Count:
                        IntrinsicItemFunctions<S>.Count(sourceItems, transformedItems);
                        break;
                    case ItemTransformFunctions.Exists:
                        IntrinsicItemFunctions<S>.Exists(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Combine:
                        IntrinsicItemFunctions<S>.Combine(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.GetPathsOfAllDirectoriesAbove:
                        IntrinsicItemFunctions<S>.GetPathsOfAllDirectoriesAbove(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.DirectoryName:
                        IntrinsicItemFunctions<S>.DirectoryName(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Metadata:
                        IntrinsicItemFunctions<S>.Metadata(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.DistinctWithCase:
                        IntrinsicItemFunctions<S>.DistinctWithCase(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Distinct:
                        IntrinsicItemFunctions<S>.Distinct(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Reverse:
                        IntrinsicItemFunctions<S>.Reverse(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ExpandQuotedExpressionFunction:
                        IntrinsicItemFunctions<S>.ExpandQuotedExpressionFunction(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ExecuteStringFunction:
                        IntrinsicItemFunctions<S>.ExecuteStringFunction(expander, elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ClearMetadata:
                        IntrinsicItemFunctions<S>.ClearMetadata(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.HasMetadata:
                        IntrinsicItemFunctions<S>.HasMetadata(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.WithMetadataValue:
                        IntrinsicItemFunctions<S>.WithMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.WithoutMetadataValue:
                        IntrinsicItemFunctions<S>.WithoutMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.AnyHaveMetadataValue:
                        IntrinsicItemFunctions<S>.AnyHaveMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    default:
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName);
                        break;
                }

                // If we have another transform, swap the source and transform lists.
                if (i < captures.Count - 1)
                {
                    (transformedItems, sourceItems) = (sourceItems, transformedItems);
                    transformedItems.Clear();
                }
            }

            // Check for break on non-empty only after ALL transforms are complete
            if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
            {
                foreach (KeyValuePair<string, S> itemTuple in transformedItems)
                {
                    if (!string.IsNullOrEmpty(itemTuple.Key))
                    {
                        brokeEarly = true;
                        return transformedItems; // break out early
                    }
                }
            }

            brokeEarly = false;
            return transformedItems;
        }

        /// <summary>
        /// Expands any item vector in the expression into items.
        ///
        /// For example, expands @(Compile->'%(foo)') to a set of items derived from the items in the "Compile" list.
        ///
        /// If there is no item vector in the expression (for example a literal "foo.cpp"), returns null.
        /// If the item vector expression expands to no items, returns an empty list.
        /// If item expansion is not allowed by the provided options, returns null.
        /// If there is an item vector but concatenated with something else, throws InvalidProjectFileException.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        ///
        /// If the expression is a transform, any transformations to an expression that evaluates to nothing (i.e., because
        /// an item has no value for a piece of metadata) are optionally indicated with a null entry in the list. This means
        /// that the length of the returned list is always the same as the length of the referenced item list in the input string.
        /// That's important for any correlation the caller wants to do.
        ///
        /// If expression was a transform, 'isTransformExpression' is true, otherwise false.
        ///
        /// Item type of the items returned is determined by the IItemFactory passed in; if the IItemFactory does not
        /// have an item type set on it, it will be given the item type of the item vector to use.
        /// </summary>
        /// <typeparam name="S">Type of the items provided by the item source used for expansion.</typeparam>
        /// <typeparam name="T">Type of the items that should be returned.</typeparam>
        internal static IList<T> ExpandSingleItemVectorExpressionIntoItems<S, T>(
            Expander<P, I> expander, string expression, IItemProvider<S> items, IItemFactory<S, T> itemFactory, ExpanderOptions options,
            bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where S : class, IItem
            where T : class, IItem
        {
            isTransformExpression = false;

            var expressionCapture = ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
            if (expressionCapture == null)
            {
                return null;
            }

            return ExpandExpressionCaptureIntoItems(expressionCapture.Value, expander, items, itemFactory, options, includeNullEntries,
                out isTransformExpression, elementLocation);
        }

        internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
            string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            if (((options & ExpanderOptions.ExpandItems) == 0) || (expression.Length == 0))
            {
                return null;
            }

            if (!expression.Contains('@'))
            {
                return null;
            }

            ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

            if (!matchesEnumerator.MoveNext())
            {
                return null;
            }

            ExpressionShredder.ItemExpressionCapture match = matchesEnumerator.Current;

            // We have a single valid @(itemlist) reference in the given expression.
            // If the passed-in expression contains exactly one item list reference,
            // with nothing else concatenated to the beginning or end, then proceed
            // with itemizing it, otherwise error.
            ProjectErrorUtilities.VerifyThrowInvalidProject(match.Value == expression, elementLocation, "EmbeddedItemVectorCannotBeItemized", expression);
            Assumed.False(matchesEnumerator.MoveNext(), "Expected just one item vector");

            return match;
        }

        internal static IList<T> ExpandExpressionCaptureIntoItems<S, T>(
            ExpressionShredder.ItemExpressionCapture expressionCapture, Expander<P, I> expander, IItemProvider<S> items, IItemFactory<S, T> itemFactory,
            ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where S : class, IItem
            where T : class, IItem
        {
            Assumed.NotNull(items, "Cannot expand items without providing items");
            isTransformExpression = false;
            bool brokeEarlyNonEmpty;

            // If the incoming factory doesn't have an item type that it can use to
            // create items, it's our indication that the caller wants its items to have the type of the
            // expression being expanded. For example, items from expanding "@(Compile") should
            // have the item type "Compile".
            if (itemFactory.ItemType == null)
            {
                itemFactory.ItemType = expressionCapture.ItemType;
            }

            IList<T> result;
            if (expressionCapture.Separator != null)
            {
                // Reference contains a separator, for example @(Compile, ';').
                // We need to flatten the list into
                // a scalar and then create a single item. Basically we need this
                // to be able to convert item lists with user specified separators into properties.
                string expandedItemVector;
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(expander, expressionCapture, items, elementLocation, builder, options);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                expandedItemVector = builder.ToString();

                result = Array.Empty<T>();

                if (expandedItemVector.Length > 0)
                {
                    T newItem = itemFactory.CreateItem(expandedItemVector, elementLocation.File);

                    result = [newItem];
                }

                return result;
            }

            List<KeyValuePair<string, S>> itemsFromCapture;
            brokeEarlyNonEmpty = ExpandExpressionCapture(expander, expressionCapture, items, elementLocation /* including null items */, options, true, out isTransformExpression, out itemsFromCapture);

            if (brokeEarlyNonEmpty)
            {
                return null;
            }

            if (itemsFromCapture == null || itemsFromCapture.Count == 0)
            {
                return Array.Empty<T>();
            }

            result = new List<T>(itemsFromCapture.Count);

            foreach (var itemTuple in itemsFromCapture)
            {
                var itemSpec = itemTuple.Key;
                var originalItem = itemTuple.Value;

                if (itemSpec != null && originalItem == null)
                {
                    // We have an itemspec, but no base item
                    result.Add(itemFactory.CreateItem(itemSpec, elementLocation.File));
                }
                else if (itemSpec != null && originalItem != null)
                {
                    result.Add(itemSpec.Equals(originalItem.EvaluatedIncludeEscaped)
                        ? itemFactory.CreateItem(originalItem, elementLocation.File) // itemspec came from direct item reference, no transforms
                        : itemFactory.CreateItem(itemSpec, originalItem, elementLocation.File)); // itemspec came from a transform and is different from its original item
                }
                else if (includeNullEntries)
                {
                    // The itemspec is null and the base item doesn't matter
                    result.Add(null);
                }
            }

            return result;
        }

        /// <summary>
        /// Expands an expression capture into a list of items
        /// If the capture uses a separator, then all the items are concatenated into one string using that separator.
        ///
        /// Returns true if ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and so it broke out early.
        /// </summary>
        /// <param name="isTransformExpression"></param>
        /// <param name="itemsFromCapture">
        /// List of items.
        ///
        /// Item1 represents the item string, escaped
        /// Item2 represents the original item.
        ///
        /// Item1 differs from Item2's string when it is coming from a transform.
        ///
        /// </param>
        /// <param name="expander">The expander whose state will be used to expand any transforms.</param>
        /// <param name="expressionCapture">The <see cref="ExpandSingleItemVectorExpressionIntoExpressionCapture"/> representing the structure of an item expression.</param>
        /// <param name="evaluatedItems"><see cref="IItemProvider{T}"/> to provide the inital items (which may get subsequently transformed, if <paramref name="expressionCapture"/> is a transform expression)>.</param>
        /// <param name="elementLocation">Location of the xml element containing the <paramref name="expressionCapture"/>.</param>
        /// <param name="options">expander options.</param>
        /// <param name="includeNullEntries">Wether to include items that evaluated to empty / null.</param>
        internal static bool ExpandExpressionCapture<S>(
            Expander<P, I> expander,
            ExpressionShredder.ItemExpressionCapture expressionCapture,
            IItemProvider<S> evaluatedItems,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<KeyValuePair<string, S>> itemsFromCapture)
            where S : class, IItem
        {
            Assumed.NotNull(evaluatedItems, "Cannot expand items without providing items");
            // There's something wrong with the expression, and we ended up with a blank item type
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(expressionCapture.ItemType), elementLocation, "InvalidFunctionPropertyExpression");

            isTransformExpression = false;

            ICollection<S> itemsOfType = evaluatedItems.GetItems(expressionCapture.ItemType);
            List<ExpressionShredder.ItemExpressionCapture> captures = expressionCapture.Captures;

            // If there are no items of the given type, then bail out early
            if (itemsOfType.Count == 0)
            {
                // ... but only if there isn't a function "Count", since that will want to return something (zero) for an empty list
                if (captures?.Any(capture => string.Equals(capture.FunctionName, "Count", StringComparison.OrdinalIgnoreCase)) != true)
                {
                    // ...or a function "AnyHaveMetadataValue", since that will want to return false for an empty list.
                    if (captures?.Any(capture => string.Equals(capture.FunctionName, "AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase)) != true)
                    {
                        itemsFromCapture = null;
                        return false;
                    }
                }
            }

            if (captures != null)
            {
                isTransformExpression = true;
            }

            if (!isTransformExpression)
            {
                itemsFromCapture = null;

                // No transform: expression is like @(Compile), so include the item spec without a transform base item
                foreach (S item in itemsOfType)
                {
                    string evaluatedIncludeEscaped = item.EvaluatedIncludeEscaped;
                    if ((evaluatedIncludeEscaped.Length > 0) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return true;
                    }

                    itemsFromCapture ??= new List<KeyValuePair<string, S>>(itemsOfType.Count);
                    itemsFromCapture.Add(new KeyValuePair<string, S>(evaluatedIncludeEscaped, item));
                }
            }
            else
            {
                // There's something wrong with the expression, and we ended up with no function names
                ProjectErrorUtilities.VerifyThrowInvalidProject(captures.Count > 0, elementLocation, "InvalidFunctionPropertyExpression");

                itemsFromCapture = Transform(expander, elementLocation, options, includeNullEntries, captures, itemsOfType, out bool brokeEarly);

                if (brokeEarly)
                {
                    return true;
                }
            }

            if (expressionCapture.Separator != null)
            {
                var joinedItems = string.Join(expressionCapture.Separator, itemsFromCapture.Select(i => i.Key));
                itemsFromCapture.Clear();
                itemsFromCapture.Add(new KeyValuePair<string, S>(joinedItems, null));
            }

            return false; // did not break early
        }

        /// <summary>
        /// Expands all item vectors embedded in the given expression into a single string.
        /// If the expression is empty, returns empty string.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        /// </summary>
        /// <typeparam name="T">Type of the items provided.</typeparam>
        internal static string ExpandItemVectorsIntoString<T>(Expander<P, I> expander, string expression, IItemProvider<T> items, ExpanderOptions options, IElementLocation elementLocation)
            where T : class, IItem
        {
            if ((options & ExpanderOptions.ExpandItems) == 0 || expression.Length == 0)
            {
                return expression;
            }

            Assumed.NotNull(items, "Cannot expand items without providing items");

            ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

            if (!matchesEnumerator.MoveNext())
            {
                return expression;
            }

            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            // As we walk through the matches, we need to copy out the original parts of the string which
            // are not covered by the match.  This preserves original behavior which did not trim whitespace
            // from between separators.
            int lastStringIndex = 0;
            do
            {
                ExpressionShredder.ItemExpressionCapture currentItem = matchesEnumerator.Current;
                if (currentItem.Index > lastStringIndex)
                {
                    if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return null;
                    }

                    builder.Append(expression, lastStringIndex, currentItem.Index - lastStringIndex);
                }

                bool brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(expander, currentItem, items, elementLocation, builder, options);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                lastStringIndex = currentItem.Index + currentItem.Length;
            }
            while (matchesEnumerator.MoveNext());

            builder.Append(expression, lastStringIndex, expression.Length - lastStringIndex);

            return builder.ToString();
        }

        /// <summary>
        /// Expand the match provided into a string, and append that to the provided InternableString.
        /// Returns true if ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and so it broke out early.
        /// </summary>
        /// <typeparam name="S">Type of source items.</typeparam>
        private static bool ExpandExpressionCaptureIntoStringBuilder<S>(
            Expander<P, I> expander,
            ExpressionShredder.ItemExpressionCapture capture,
            IItemProvider<S> evaluatedItems,
            IElementLocation elementLocation,
            SpanBasedStringBuilder builder,
            ExpanderOptions options)
            where S : class, IItem
        {
            List<KeyValuePair<string, S>> itemsFromCapture;
            bool throwaway;
            var brokeEarlyNonEmpty = ExpandExpressionCapture(expander, capture, evaluatedItems, elementLocation /* including null items */, options, true, out throwaway, out itemsFromCapture);

            if (brokeEarlyNonEmpty)
            {
                return true;
            }

            if (itemsFromCapture == null)
            {
                // No items to expand.
                return false;
            }

            int startLength = builder.Length;
            bool truncate = IsTruncationEnabled(options);

            // if the capture.Separator is not null, then ExpandExpressionCapture would have joined the items using that separator itself
            for (int i = 0; i < itemsFromCapture.Count; i++)
            {
                var item = itemsFromCapture[i];
                if (truncate)
                {
                    if (i >= ItemLimitPerExpansion)
                    {
                        builder.Append("...");
                        return false;
                    }
                    int currentLength = builder.Length - startLength;
                    if (!string.IsNullOrEmpty(item.Key) && currentLength + item.Key.Length > CharacterLimitPerExpansion)
                    {
                        int truncateIndex = CharacterLimitPerExpansion - currentLength - 3;
                        if (truncateIndex > 0)
                        {
                            builder.Append(item.Key, 0, truncateIndex);
                        }
                        builder.Append("...");
                        return false;
                    }
                }
                builder.Append(item.Key);
                if (i < itemsFromCapture.Count - 1)
                {
                    builder.Append(";");
                }
            }

            return false;
        }
    }
}
