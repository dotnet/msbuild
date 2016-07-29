// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Expands item/property/metadata in expressions.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Win32;
using AvailableStaticMethods = Microsoft.Build.Internal.AvailableStaticMethods;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using Microsoft.Build.Collections;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Indicates to the expander what exactly it should expand.
    /// </summary>
    [Flags]
    internal enum ExpanderOptions
    {
        /// <summary>
        /// Invalid
        /// </summary>
        Invalid = 0x0,

        /// <summary>
        /// Expand bare custom metadata, like %(foo), but not built-in
        /// metadata, such as %(filename) or %(identity)
        /// </summary>
        ExpandCustomMetadata = 0x1,

        /// <summary>
        /// Expand bare built-in metadata, such as %(filename) or %(identity)
        /// </summary>
        ExpandBuiltInMetadata = 0x2,

        /// <summary>
        /// Expand all bare metadata
        /// </summary>
        ExpandMetadata = ExpandCustomMetadata | ExpandBuiltInMetadata,

        /// <summary>
        /// Expand only properties
        /// </summary>
        ExpandProperties = 0x4,

        /// <summary>
        /// Expand only item list expressions
        /// </summary>
        ExpandItems = 0x8,

        /// <summary>
        /// If the expression is going to not be an empty string, break
        /// out early
        /// </summary>
        BreakOnNotEmpty = 0x10,

        /// <summary>
        /// Expand only properties and then item lists
        /// </summary>
        ExpandPropertiesAndItems = ExpandProperties | ExpandItems,

        /// <summary>
        /// Expand only bare metadata and then properties
        /// </summary>
        ExpandPropertiesAndMetadata = ExpandMetadata | ExpandProperties,

        /// <summary>
        /// Expand only bare custom metadata and then properties
        /// </summary>
        ExpandPropertiesAndCustomMetadata = ExpandCustomMetadata | ExpandProperties,

        /// <summary>
        /// Expand bare metadata, then properties, then item expressions
        /// </summary>
        ExpandAll = ExpandMetadata | ExpandProperties | ExpandItems
    }

    /// <summary>
    /// Expands item/property/metadata in expressions.
    /// Encapsulates the data necessary for expansion.
    /// </summary>
    /// <remarks>
    /// Requires the caller to explicitly state what they wish to expand at the point of expansion (explicitly does not have a field for ExpanderOptions). 
    /// Callers typically use a single expander in many locations, and this forces the caller to make explicit what they wish to expand at the point of expansion.
    /// 
    /// Requires the caller to have previously provided the necessary material for the expansion requested.
    /// For example, if the caller requests ExpanderOptions.ExpandItems, the Expander will throw if it was not given items.
    /// </remarks>
    /// <typeparam name="P">Type of the properties used</typeparam>
    /// <typeparam name="I">Type of the items used.</typeparam>
    internal class Expander<P, I>
        where P : class, IProperty
        where I : class, IItem
    {
        /// <summary>
        /// Those characters which indicate that an expression may contain expandable
        /// expressions
        /// </summary>
        private static char[] s_expandableChars = { '$', '%', '@' };

        /// <summary>
        /// The CultureInfo from the invariant culture. Used to avoid allocations for
        /// perfoming IndexOf etc.
        /// </summary>
        private static CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

        /// <summary>
        /// Properties to draw on for expansion
        /// </summary>
        private IPropertyProvider<P> _properties;

        /// <summary>
        /// Items to draw on for expansion
        /// </summary>
        private IItemProvider<I> _items;

        /// <summary>
        /// Metadata to draw on for expansion
        /// </summary>
        private IMetadataTable _metadata;

        /// <summary>
        /// Set of properties which are null during expansion
        /// </summary>
        private UsedUninitializedProperties _usedUninitializedProperties;

        /// <summary>
        /// Creates an expander passing it some properties to use.
        /// Properties may be null.
        /// </summary>
        internal Expander(IPropertyProvider<P> properties)
        {
            _properties = properties;
            _usedUninitializedProperties = new UsedUninitializedProperties();
        }

        /// <summary>
        /// Creates an expander passing it some properties and items to use.
        /// Either or both may be null.
        /// </summary>
        internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items)
            : this(properties)
        {
            _items = items;
        }

        /// <summary>
        /// Creates an expander passing it some properties, items, and/or metadata to use.
        /// Any or all may be null.
        /// </summary>
        internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IMetadataTable metadata)
            : this(properties, items)
        {
            _metadata = metadata;
        }

        /// <summary>
        /// Whether to warn when we set a property for the first time, after it was previously used.
        /// Default is false, unless MSBUILDWARNONUNINITIALIZEDPROPERTY is set.
        /// </summary>
        internal bool WarnForUninitializedProperties
        {
            get { return _usedUninitializedProperties.Warn; }
            set { _usedUninitializedProperties.Warn = value; }
        }

        /// <summary>
        /// Accessor for the metadata.
        /// Set temporarily during item metadata evaluation.
        /// </summary>
        internal IMetadataTable Metadata
        {
            get { return _metadata; }
            set { _metadata = value; }
        }

        /// <summary>
        /// If a property is expanded but evaluates to null then it is consisered to be un-initialized. 
        /// We want to keep track of these properties so that we can warn if the property gets set later on.
        /// </summary>
        internal UsedUninitializedProperties UsedUninitializedProperties
        {
            get { return _usedUninitializedProperties; }
            set { _usedUninitializedProperties = value; }
        }

        /// <summary>
        /// Tests to see if the expression may contain expandable expressions, i.e.
        /// contains $, % or @
        /// </summary>
        internal static bool ExpressionMayContainExpandableExpressions(string expression)
        {
            return expression.IndexOfAny(s_expandableChars) > -1;
        }

        /// <summary>
        /// Returns true if the expression contains an item vector pattern, else returns false.
        /// Used to flag use of item expressions where they are illegal.
        /// </summary>
        internal static bool ExpressionContainsItemVector(string expression)
        {
            List<ExpressionShredder.ItemExpressionCapture> transforms = ExpressionShredder.GetReferencedItemExpressions(expression);

            return (transforms != null);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
        /// This is the standard form. Before using the expanded value, it must be unescaped, and this does that for you.
        /// 
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.        
        /// </summary>
        internal string ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            string result = ExpandIntoStringLeaveEscaped(expression, options, elementLocation);

            result = (result == null) ? null : EscapingUtilities.UnescapeAll(result);

            return result;
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
        /// Use this form when the result is going to be processed further, for example by matching against the file system,
        /// so literals must be distinguished, and you promise to unescape after that.
        /// 
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        /// </summary>
        internal string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            if (expression.Length == 0)
            {
                return String.Empty;
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation, "elementLocation");

            string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options);
            result = PropertyExpander<P>.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _usedUninitializedProperties);
            result = ItemExpander.ExpandItemVectorsIntoString<I>(this, result, _items, options, elementLocation);

            return result;
        }

        /// <summary>
        /// Used only for unit tests. Expands the property expression (including any metadata expressions) and returns
        /// the result typed (i.e. not converted into a string if the result is a function return)
        /// </summary>
        internal object ExpandPropertiesLeaveTypedAndEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            if (expression.Length == 0)
            {
                return String.Empty;
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation, "elementLocation");

            string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options);
            return PropertyExpander<P>.ExpandPropertiesLeaveTypedAndEscaped(metaExpanded, _properties, options, elementLocation, _usedUninitializedProperties);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options,
        /// then splits on semi-colons into a list of strings.
        /// Use this form when the result is going to be processed further, for example by matching against the file system,
        /// so literals must be distinguished, and you promise to unescape after that.
        /// </summary>
        internal IList<string> ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            ErrorUtilities.VerifyThrow((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

            return ExpressionShredder.SplitSemiColonSeparatedList(ExpandIntoStringLeaveEscaped(expression, options, elementLocation));
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
        /// and produces a list of TaskItems.
        /// If the expression is empty, returns an empty list.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        /// </summary>
        internal IList<TaskItem> ExpandIntoTaskItemsLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            return ExpandIntoItemsLeaveEscaped(expression, (IItemFactory<I, TaskItem>)TaskItemFactory.Instance, options, elementLocation);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
        /// and produces a list of items of the type for which it was specialized.
        /// If the expression is empty, returns an empty list.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        /// 
        /// Use this form when the result is going to be processed further, for example by matching against the file system,
        /// so literals must be distinguished, and you promise to unescape after that.
        /// </summary>
        /// <typeparam name="T">Type of items to return</typeparam>
        internal IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
            where T : class, IItem
        {
            if (expression.Length == 0)
            {
                return ReadOnlyEmptyList<T>.Instance;
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation, "elementLocation");

            expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options);
            expression = PropertyExpander<P>.ExpandPropertiesLeaveEscaped(expression, _properties, options, elementLocation, _usedUninitializedProperties);

            List<T> result = new List<T>();

            if (expression.Length == 0)
            {
                return result;
            }

            IList<string> splits = ExpressionShredder.SplitSemiColonSeparatedList(expression);

            foreach (string split in splits)
            {
                bool isTransformExpression;
                IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems<I, T>(this, split, _items, itemFactory, options, false /* do not include null items */, out isTransformExpression, elementLocation);

                if ((itemsToAdd == null /* broke out early non empty */ || (itemsToAdd.Count > 0)) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                {
                    return null;
                }

                if (itemsToAdd != null)
                {
                    result.AddRange(itemsToAdd);
                }
                else
                {
                    // The expression is not of the form @(itemName).  Therefore, just
                    // treat it as a string, and create a new item from that string.
                    T itemToAdd = itemFactory.CreateItem(split, elementLocation.File);

                    result.Add(itemToAdd);
                }
            }

            return result;
        }

        /// <summary>
        /// This is a specialized method for the use of TargetUpToDateChecker and Evaluator.EvaluateItemXml only.
        /// 
        /// Extracts the items in the given SINGLE item vector.
        /// For example, expands @(Compile->'%(foo)') to a set of items derived from the items in the "Compile" list.
        ///
        /// If there is in fact more than one vector in the expression, throws InvalidProjectFileException.
        /// 
        /// If there are no item expressions in the expression (for example a literal "foo.cpp"), returns null.
        /// If expression expands to no items, returns an empty list.
        /// If item expansion is not allowed by the provided options, returns null.
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
        /// <typeparam name="T">Type of the items that should be returned</typeparam>
        internal IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            if (expression.Length == 0)
            {
                isTransformExpression = false;
                return ReadOnlyEmptyList<T>.Instance;
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation, "elementLocation");

            return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, expression, _items, itemFactory, options, includeNullItems, out isTransformExpression, elementLocation);
        }

        internal static ExpressionShredder.ItemExpressionCapture ExpandSingleItemVectorExpressionIntoExpressionCapture(
                string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            return ItemExpander.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
        }

        internal IList<T> ExpandExpressionCaptureIntoItems<S, T>(
                ExpressionShredder.ItemExpressionCapture expressionCapture, IItemProvider<S> items, IItemFactory<S, T> itemFactory,
                ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where S : class, IItem
            where T : class, IItem
        {
            return ItemExpander.ExpandExpressionCaptureIntoItems<S, T>(expressionCapture, this, items, itemFactory, options,
                includeNullEntries, out isTransformExpression, elementLocation);
        }

        /// <summary>
        /// Returns true if the supplied string contains a valid property name
        /// </summary>
        private static bool IsValidPropertyName(string propertyName)
        {
            if (propertyName.Length == 0 || !XmlUtilities.IsValidInitialElementNameCharacter(propertyName[0]))
            {
                return false;
            }

            for (int n = 1; n < propertyName.Length; n++)
            {
                if (!XmlUtilities.IsValidSubsequentElementNameCharacter(propertyName[n]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Scan for the closing bracket that matches the one we've already skipped;
        /// essentially, pushes and pops on a stack of parentheses to do this.
        /// Takes the expression and the index to start at.
        /// Returns the index of the matching parenthesis, or -1 if it was not found.
        /// </summary>
        private static int ScanForClosingParenthesis(string expression, int index)
        {
            bool potentialPropertyFunction = false;
            bool potentialRegistryFunction = false;
            return ScanForClosingParenthesis(expression, index, out potentialPropertyFunction, out potentialRegistryFunction);
        }

        /// <summary>
        /// Scan for the closing bracket that matches the one we've already skipped;
        /// essentially, pushes and pops on a stack of parentheses to do this.
        /// Takes the expression and the index to start at.
        /// Returns the index of the matching parenthesis, or -1 if it was not found.
        /// Also returns flags to indicate if a propertyfunction or registry property is likely
        /// to be found in the expression
        /// </summary>
        private static int ScanForClosingParenthesis(string expression, int index, out bool potentialPropertyFunction, out bool potentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            potentialPropertyFunction = false;
            potentialRegistryFunction = false;

            unsafe
            {
                fixed (char* pchar = expression)
                {
                    // Scan for our closing ')'
                    while (index < length && nestLevel > 0)
                    {
                        char character = pchar[index];

                        if (character == '\'' || character == '`' || character == '"')
                        {
                            index++;
                            index = ScanForClosingQuote(character, expression, index);

                            if (index < 0)
                            {
                                return -1;
                            }
                        }
                        else if (character == '(')
                        {
                            nestLevel++;
                        }
                        else if (character == ')')
                        {
                            nestLevel--;
                        }
                        else if (character == '.' || character == '[' || character == '$')
                        {
                            potentialPropertyFunction = true;
                        }
                        else if (character == ':')
                        {
                            potentialRegistryFunction = true;
                        }

                        index++;
                    }
                }
            }

            // We will have parsed past the ')', so step back one character
            index--;

            return (nestLevel == 0) ? index : -1;
        }

        /// <summary>
        /// Skip all characters until we find the matching quote character
        /// </summary>
        private static int ScanForClosingQuote(char quoteChar, string expression, int index)
        {
            unsafe
            {
                fixed (char* pchar = expression)
                {
                    // Scan for our closing quoteChar
                    while (index < expression.Length)
                    {
                        if (pchar[index] == quoteChar)
                        {
                            return index;
                        }

                        index++;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Add the argument in the StringBuilder to the arguments list, handling nulls
        /// appropriately
        /// </summary>
        private static void AddArgument(List<string> arguments, ReuseableStringBuilder argumentBuilder)
        {
            // If we don't have something that can be treated as an argument
            // then we should treat it as a null so that passing nulls
            // becomes possible through an empty argument between commas.
            ErrorUtilities.VerifyThrowArgumentNull(argumentBuilder, "argumentBuilder");

            // we reached the end of an argument, add the builder's final result
            // to our arguments. 
            string argValue = OpportunisticIntern.InternableToString(argumentBuilder).Trim();

            // We support passing of null through the argument constant value null
            if (String.Compare("null", argValue, StringComparison.OrdinalIgnoreCase) == 0)
            {
                arguments.Add(null);
            }
            else
            {
                if (argValue.Length > 0)
                {
                    if (argValue[0] == '\'' && argValue[argValue.Length - 1] == '\'')
                    {
                        arguments.Add(argValue.Trim('\''));
                    }
                    else if (argValue[0] == '`' && argValue[argValue.Length - 1] == '`')
                    {
                        arguments.Add(argValue.Trim('`'));
                    }
                    else if (argValue[0] == '"' && argValue[argValue.Length - 1] == '"')
                    {
                        arguments.Add(argValue.Trim('"'));
                    }
                    else
                    {
                        arguments.Add(argValue);
                    }
                }
                else
                {
                    arguments.Add(argValue);
                }
            }
        }

        /// <summary>
        /// Extract the first level of arguments from the content.
        /// Splits the content passed in at commas.
        /// Returns an array of unexpanded arguments.
        /// If there are no arguments, returns an empty array.
        /// </summary>
        private static string[] ExtractFunctionArguments(IElementLocation elementLocation, string expressionFunction, string argumentsString)
        {
            int argumentsContentLength = argumentsString.Length;

            List<string> arguments = new List<string>();

            // With the reuseable string builder, there's no particular need to initialize the length as it will already have grown.
            using (var argumentBuilder = new ReuseableStringBuilder())
            {
                unsafe
                {
                    fixed (char* argumentsContent = argumentsString)
                    {
                        // Iterate over the contents of the arguments extracting the
                        // the individual arguments as we go
                        for (int n = 0; n < argumentsContentLength; n++)
                        {
                            // We found a property expression.. skip over all of it.
                            if ((n < argumentsContentLength - 1) && (argumentsContent[n] == '$' && argumentsContent[n + 1] == '('))
                            {
                                int nestedPropertyStart = n;
                                n += 2; // skip over the opening '$('

                                // Scan for the matching closing bracket, skipping any nested ones
                                n = ScanForClosingParenthesis(argumentsString, n);

                                if (n == -1)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                                }

                                argumentBuilder.Append(argumentsString.Substring(nestedPropertyStart, (n - nestedPropertyStart) + 1));
                            }
                            else if (argumentsContent[n] == '`' || argumentsContent[n] == '"' || argumentsContent[n] == '\'')
                            {
                                int quoteStart = n;
                                n += 1; // skip over the opening quote

                                n = ScanForClosingQuote(argumentsString[quoteStart], argumentsString, n);

                                if (n == -1)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedQuote"));
                                }

                                argumentBuilder.Append(argumentsString.Substring(quoteStart, (n - quoteStart) + 1));
                            }
                            else if (argumentsContent[n] == ',')
                            {
                                // We have reached the end of the current argument, go ahead and add it
                                // to our list
                                AddArgument(arguments, argumentBuilder);

                                // Clear out the argument builder ready for the next argument
                                argumentBuilder.Remove(0, argumentBuilder.Length);
                            }
                            else
                            {
                                argumentBuilder.Append(argumentsContent[n]);
                            }
                        }
                    }
                }

                // This will either be the one and only argument, or the last one
                // so add it to our list
                AddArgument(arguments, argumentBuilder);
            }

            return arguments.ToArray();
        }

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
            /// Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile)
            /// </summary>
            /// <param name="expression">The expression containing item metadata references</param>
            /// <returns>The string with item metadata expanded in-place, escaped.</returns>
            internal static string ExpandMetadataLeaveEscaped(string expression, IMetadataTable metadata, ExpanderOptions options)
            {
                if (((options & ExpanderOptions.ExpandMetadata) == 0))
                {
                    return expression;
                }

                if (expression.Length == 0)
                {
                    return expression;
                }

                ErrorUtilities.VerifyThrow(metadata != null, "Cannot expand metadata without providing metadata");

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
                    MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options);
                    result = RegularExpressions.ItemMetadataPattern.Replace(expression, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));
                }
                else
                {
                    List<ExpressionShredder.ItemExpressionCapture> itemVectorExpressions = ExpressionShredder.GetReferencedItemExpressions(expression);

                    // The most common case is where the transform is the whole expression
                    // Also if there were no valid item vector expressions found, then go ahead and do the replacement on
                    // the whole expression (which is what Orcas did).
                    if (itemVectorExpressions != null && itemVectorExpressions.Count == 1 && itemVectorExpressions[0].Value == expression && itemVectorExpressions[0].Separator == null)
                    {
                        return expression;
                    }

                    // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                    // With the reuseable string builder, there's no particular need to initialize the length as it will already have grown.
                    using (var finalResultBuilder = new ReuseableStringBuilder())
                    {
                        int start = 0;
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options);

                        if (itemVectorExpressions != null)
                        {
                            // Move over the expression, skipping those that have been recognized as an item vector expression
                            // Anything other than an item vector expression we want to expand bare metadata in.
                            for (int n = 0; n < itemVectorExpressions.Count; n++)
                            {
                                string vectorExpression = itemVectorExpressions[n].Value;

                                // Extract the part of the expression that appears before the item vector expression
                                // e.g. the ABC in ABC@(foo->'%(FullPath)')
                                string subExpressionToReplaceIn = expression.Substring(start, itemVectorExpressions[n].Index - start);
                                string replacementResult = RegularExpressions.NonTransformItemMetadataPattern.Replace(subExpressionToReplaceIn, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));

                                // Append the metadata replacement
                                finalResultBuilder.Append(replacementResult);

                                // Expand any metadata that appears in the item vector expression's separator
                                if (itemVectorExpressions[n].Separator != null)
                                {
                                    vectorExpression = RegularExpressions.NonTransformItemMetadataPattern.Replace(itemVectorExpressions[n].Value, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata), -1, itemVectorExpressions[n].SeparatorStart);
                                }

                                // Append the item vector expression as is
                                // e.g. the @(foo->'%(FullPath)') in ABC@(foo->'%(FullPath)')
                                finalResultBuilder.Append(vectorExpression);

                                // Move onto the next part of the expression that isn't an item vector expression
                                start = (itemVectorExpressions[n].Index + itemVectorExpressions[n].Length);
                            }
                        }

                        // If there's anything left after the last item vector expression
                        // then we need to metadata replace and then append that
                        if (start < expression.Length)
                        {
                            string subExpressionToReplaceIn = expression.Substring(start);
                            string replacementResult = RegularExpressions.NonTransformItemMetadataPattern.Replace(subExpressionToReplaceIn, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));

                            finalResultBuilder.Append(replacementResult);
                        }

                        result = OpportunisticIntern.InternableToString(finalResultBuilder);
                    }
                }

                // Don't create more strings
                if (String.Equals(result, expression, StringComparison.Ordinal))
                {
                    result = expression;
                }

                return result;
            }

            /// <summary>
            /// A functor that returns the value of the metadata in the match
            /// that is contained in the metadata dictionary it was created with.
            /// </summary>
            private class MetadataMatchEvaluator
            {
                /// <summary>
                /// Source of the metadata
                /// </summary>
                private IMetadataTable _metadata;

                /// <summary>
                /// Whether to expand built-in metadata, custom metadata, or both kinds.
                /// </summary>
                private ExpanderOptions _options;

                /// <summary>
                /// Constructor taking a source of metadata
                /// </summary>
                internal MetadataMatchEvaluator(IMetadataTable metadata, ExpanderOptions options)
                {
                    _metadata = metadata;
                    _options = (options & ExpanderOptions.ExpandMetadata);

                    ErrorUtilities.VerifyThrow(options != ExpanderOptions.Invalid, "Must be expanding metadata of some kind");
                }

                /// <summary>
                /// Expands a single item metadata, which may be qualified with an item type.
                /// </summary>
                internal string ExpandSingleMetadata(Match itemMetadataMatch)
                {
                    ErrorUtilities.VerifyThrow(itemMetadataMatch.Success, "Need a valid item metadata.");

                    string metadataName = itemMetadataMatch.Groups[RegularExpressions.NameGroup].Value;
                    string itemType = null;

                    // check if the metadata is qualified with the item type
                    if (itemMetadataMatch.Groups[RegularExpressions.ItemSpecificationGroup].Length > 0)
                    {
                        itemType = itemMetadataMatch.Groups[RegularExpressions.ItemTypeGroup].Value;
                    }

                    // look up the metadata - we may not have a value for it
                    string metadataValue = itemMetadataMatch.Value;

                    bool isBuiltInMetadata = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName);

                    if (
                        (isBuiltInMetadata && ((_options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
                       (!isBuiltInMetadata && ((_options & ExpanderOptions.ExpandCustomMetadata) != 0))
                        )
                    {
                        metadataValue = _metadata.GetEscapedValue(itemType, metadataName);
                    }

                    return metadataValue;
                }
            }
        }

        /// <summary>
        /// Expands property expressions, like $(Configuration) and $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
        /// </summary>
        /// <remarks>
        /// This is a private nested class, exposed only through the Expander class.
        /// That allows it to hide its private methods even from Expander.
        /// </remarks>
        /// <typeparam name="T">Type of the properties used to expand the expression</typeparam>
        private static class PropertyExpander<T>
            where T : class, IProperty
        {
            /// <summary>
            /// This method takes a string which may contain any number of
            /// "$(propertyname)" tags in it.  It replaces all those tags with
            /// the actual property values, and returns a new string.  For example,
            ///
            ///     string processedString =
            ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
            ///
            /// This code might produce:
            ///
            ///     processedString = "Value of NoLogo is true."
            ///
            /// If the sourceString contains an embedded property which doesn't
            /// have a value, then we replace that tag with an empty string.
            ///
            /// This method leaves the result escaped.  Callers may need to unescape on their own as appropriate.
            /// </summary>
            internal static string ExpandPropertiesLeaveEscaped(string expression, IPropertyProvider<T> properties, ExpanderOptions options, IElementLocation elementLocation, UsedUninitializedProperties usedUninitializedProperties)
            {
                return ConvertToString(ExpandPropertiesLeaveTypedAndEscaped(expression, properties, options, elementLocation, usedUninitializedProperties));
            }

            /// <summary>
            /// This method takes a string which may contain any number of
            /// "$(propertyname)" tags in it.  It replaces all those tags with
            /// the actual property values, and returns a new string.  For example,
            ///
            ///     string processedString =
            ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
            ///
            /// This code might produce:
            ///
            ///     processedString = "Value of NoLogo is true."
            ///
            /// If the sourceString contains an embedded property which doesn't
            /// have a value, then we replace that tag with an empty string.
            ///
            /// This method leaves the result typed and escaped.  Callers may need to convert to string, and unescape on their own as appropriate.
            /// </summary>
            internal static object ExpandPropertiesLeaveTypedAndEscaped(string expression, IPropertyProvider<T> properties, ExpanderOptions options, IElementLocation elementLocation, UsedUninitializedProperties usedUninitializedProperties)
            {
                if (((options & ExpanderOptions.ExpandProperties) == 0) || String.IsNullOrEmpty(expression))
                {
                    return expression;
                }

                ErrorUtilities.VerifyThrow(properties != null, "Cannot expand properties without providing properties");

                // These are also zero-based indices into the expression, but
                // these tell us where the current property tag begins and ends.
                int propertyStartIndex, propertyEndIndex;

                // If there are no substitutions, then just return the string.
                propertyStartIndex = s_invariantCompareInfo.IndexOf(expression, "$(", CompareOptions.Ordinal);
                if (propertyStartIndex == -1)
                {
                    return expression;
                }

                // We will build our set of results as object components
                // so that we can either maintain the object's type in the event
                // that we have a single component, or convert to a string
                // if concatenation is required.
                List<object> results = null;
                object lastResult = null;

                // The sourceIndex is the zero-based index into the expression,
                // where we've essentially read up to and copied into the target string.
                int sourceIndex = 0;
                int expressionLength = expression.Length;

                // Search for "$(" in the expression.  Loop until we don't find it 
                // any more.
                while (propertyStartIndex != -1)
                {
                    if (lastResult != null)
                    {
                        if (results == null)
                        {
                            results = new List<object>(4);
                        }

                        results.Add(lastResult);
                    }

                    bool tryExtractPropertyFunction = false;
                    bool tryExtractRegistryFunction = false;

                    // Append the result with the portion of the expression up to
                    // (but not including) the "$(", and advance the sourceIndex pointer.
                    if (propertyStartIndex - sourceIndex > 0)
                    {
                        if (results == null)
                        {
                            results = new List<object>(4);
                        }

                        results.Add(expression.Substring(sourceIndex, propertyStartIndex - sourceIndex));
                    }

                    sourceIndex = propertyStartIndex;

                    // Following the "$(" we need to locate the matching ')'
                    // Scan for the matching closing bracket, skipping any nested ones
                    // This is a very complete, fast validation of parenthesis matching including for nested
                    // function calls.
                    propertyEndIndex = ScanForClosingParenthesis(expression, propertyStartIndex + 2, out tryExtractPropertyFunction, out tryExtractRegistryFunction);

                    if (propertyEndIndex == -1)
                    {
                        // If we didn't find the closing parenthesis, that means this
                        // isn't really a well-formed property tag.  Just literally
                        // copy the remainder of the expression (starting with the "$("
                        // that we found) into the result, and quit.
                        lastResult = expression.Substring(propertyStartIndex, expression.Length - propertyStartIndex);
                        sourceIndex = expression.Length;
                    }
                    else
                    {
                        // Aha, we found the closing parenthesis.  All the stuff in
                        // between the "$(" and the ")" constitutes the property body.
                        // Note: Current propertyStartIndex points to the "$", and 
                        // propertyEndIndex points to the ")".  That's why we have to
                        // add 2 for the start of the substring, and subtract 2 for 
                        // the length.
                        string propertyBody;

                        // A property value of null will indicate that we're calling a static function on a type
                        object propertyValue = null;

                        // Compat: $() should return String.Empty
                        if (propertyStartIndex + 2 == propertyEndIndex)
                        {
                            propertyValue = String.Empty;
                        }
                        else if ((expression.Length - (propertyStartIndex + 2)) > 9 && tryExtractRegistryFunction && s_invariantCompareInfo.IndexOf(expression, "Registry:", propertyStartIndex + 2, 9, CompareOptions.OrdinalIgnoreCase) == propertyStartIndex + 2)
                        {
                            propertyBody = expression.Substring(propertyStartIndex + 2, propertyEndIndex - propertyStartIndex - 2);

                            // If the property body starts with any of our special objects, then deal with them
                            // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                            propertyValue = ExpandRegistryValue(propertyBody, elementLocation);
                        }

                        // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return String.Empty
                        // In this case, tryExtractRegistryFunction will be false. Note that very few properties are exactly 77 chars, so this check should be fast.
                        else if ((propertyEndIndex - (propertyStartIndex + 2)) == 77 && s_invariantCompareInfo.IndexOf(expression, @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory", propertyStartIndex + 2, 77, CompareOptions.OrdinalIgnoreCase) == propertyStartIndex + 2)
                        {
                            propertyValue = String.Empty;
                        }

                        // Compat hack: WebProjects may have an import with a condition like:
                        //       Condition=" '$(Solutions.VSVersion)' == '8.0'" 
                        // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
                        // Be compatible by returning an empty string here.
                        else if ((propertyEndIndex - (propertyStartIndex + 2)) == 19 && String.Equals(expression, "$(Solutions.VSVersion)", StringComparison.Ordinal))
                        {
                            propertyValue = String.Empty;
                        }
                        else if (tryExtractPropertyFunction)
                        {
                            propertyBody = expression.Substring(propertyStartIndex + 2, propertyEndIndex - propertyStartIndex - 2);

                            // This is likely to be a function expression
                            propertyValue = ExpandPropertyBody(propertyBody, propertyValue, properties, options, elementLocation, usedUninitializedProperties);
                        }
                        else // This is a regular property
                        {
                            propertyValue = LookupProperty(properties, expression, propertyStartIndex + 2, propertyEndIndex - 1, elementLocation, usedUninitializedProperties);
                        }

                        // Record our result, and advance
                        // our sourceIndex pointer to the character just after the closing
                        // parenthesis.
                        lastResult = propertyValue;
                        sourceIndex = propertyEndIndex + 1;
                    }

                    propertyStartIndex = s_invariantCompareInfo.IndexOf(expression, "$(", sourceIndex, CompareOptions.Ordinal);
                }

                // If we have only a single result, then just return it
                if (results == null && expression.Length == sourceIndex)
                {
                    return lastResult;
                }
                else
                {
                    // The expression is constant, return it as is
                    if (sourceIndex == 0)
                    {
                        return expression;
                    }

                    // We have more than one result collected, therefore we need to concatenate
                    // into the final result string. This does mean that we will lose type information.
                    // However since the user wanted contatenation, then they clearly wanted that to happen.

                    // Initialize our output string to empty string.
                    // This method is called very often - of the order of 3,000 times per project.
                    // With the reuseable string builder, there's no particular need to initialize the length as it will already have grown.
                    using (var result = new ReuseableStringBuilder())
                    {
                        // Append our collected results
                        if (results != null)
                        {
                            // Create a combined result string from the result components that we've gathered
                            foreach (object component in results)
                            {
                                result.Append(component.ToString());
                            }
                        }

                        // Append the last result we collected (it wasn't added to the list)
                        if (lastResult != null)
                        {
                            result.Append(lastResult.ToString());
                        }

                        // And if we couldn't find anymore property tags in the expression,
                        // so just literally copy the remainder into the result.
                        if (expression.Length - sourceIndex > 0)
                        {
                            result.Append(expression, sourceIndex, expression.Length - sourceIndex);
                        }

                        return OpportunisticIntern.InternableToString(result);
                    }
                }
            }

            /// <summary>
            /// Expand the body of the property, including any functions that it may contain
            /// </summary>
            internal static object ExpandPropertyBody(string propertyBody, object propertyValue, IPropertyProvider<T> properties, ExpanderOptions options, IElementLocation elementLocation, UsedUninitializedProperties usedUninitializedProperties)
            {
                Function<T> function = null;
                string propertyName = propertyBody;

                // Trim the body for compatibility reasons:
                // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
                // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
                // But we will do a property name lookup on the propertyName that we held onto.
                if (Char.IsWhiteSpace(propertyBody[0]) || Char.IsWhiteSpace(propertyBody[propertyBody.Length - 1]))
                {
                    propertyBody = propertyBody.Trim();
                }

                // If we don't have a clean propertybody then we'll do deeper checks to see
                // if what we have is a function
                if (!IsValidPropertyName(propertyBody))
                {
                    if (propertyBody.Contains(".") || propertyBody[0] == '[')
                    {
                        if (BuildParameters.DebugExpansion)
                        {
                            Console.WriteLine("Expanding: {0}", propertyBody);
                        }

                        // This is a function
                        function = Function<T>.ExtractPropertyFunction(propertyBody, elementLocation, propertyValue, usedUninitializedProperties);

                        // We may not have been able to parse out a function
                        if (function != null)
                        {
                            // We will have either extracted the actual property name
                            // or realised that there is none (static function), and have recorded a null
                            propertyName = function.ExpressionRootName;
                        }
                        else
                        {
                            // In the event that we have been handed an unrecognized property body, throw
                            // an invalid function property exception.
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                            return null;
                        }
                    }
                    else if (propertyValue == null && propertyBody.Contains("[")) // a single property indexer
                    {
                        int indexerStart = propertyBody.IndexOf('[');
                        int indexerEnd = propertyBody.IndexOf(']');

                        if (indexerStart < 0 || indexerEnd < 0)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                        }
                        else
                        {
                            propertyValue = LookupProperty(properties, propertyBody, 0, indexerStart - 1, elementLocation, usedUninitializedProperties);
                            propertyBody = propertyBody.Substring(indexerStart);

                            // recurse so that the function representing the indexer can be executed on the property value
                            return ExpandPropertyBody(propertyBody, propertyValue, properties, options, elementLocation, usedUninitializedProperties);
                        }
                    }
                    else
                    {
                        // In the event that we have been handed an unrecognized property body, throw
                        // an invalid function property exception.
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                        return null;
                    }
                }

                // Find the property value in our property collection.  This 
                // will automatically return "" (empty string) if the property
                // doesn't exist in the collection, and we're not executing a static function
                if (!String.IsNullOrEmpty(propertyName))
                {
                    propertyValue = LookupProperty(properties, propertyName, elementLocation, usedUninitializedProperties);
                }

                if (function != null)
                {
                    // Because of the rich expansion capabilities of MSBuild, we need to keep things
                    // as strings, since property expansion & string embedding can happen anywhere
                    // propertyValue can be null here, when we're invoking a static function
                    propertyValue = function.Execute(propertyValue, properties, options, elementLocation);
                }

                return propertyValue;
            }

            /// <summary>
            /// Convert the object into an MSBuild friendly string
            /// Arrays are supported.
            /// Will not return NULL
            /// </summary>
            internal static string ConvertToString(object valueToConvert)
            {
                if (valueToConvert != null)
                {
                    Type valueType = valueToConvert.GetType();
                    string convertedString;

                    // If the type is a string, then there is nothing to do
                    if (valueType == typeof(string))
                    {
                        convertedString = (string)valueToConvert;
                    }
                    else if (valueToConvert is IDictionary)
                    {
                        // If the return type is an IDictionary, then we convert this to
                        // a semi-colon delimited set of A=B pairs.
                        // Key and Value are converted to string and escaped
                        IDictionary dictionary = valueToConvert as IDictionary;
                        using (var builder = new ReuseableStringBuilder())
                        {
                            foreach (DictionaryEntry entry in dictionary)
                            {
                                if (builder.Length > 0)
                                {
                                    builder.Append(';');
                                }

                                // convert and escape each key and value in the dictionary entry
                                builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                                builder.Append('=');
                                builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                            }

                            convertedString = OpportunisticIntern.InternableToString(builder);
                        }
                    }
                    else if (valueToConvert is IEnumerable)
                    {
                        // If the return is enumerable, then we'll convert to semi-colon delimited elements
                        // each of which must be converted, so we'll recurse for each element
                        using (var builder = new ReuseableStringBuilder())
                        {
                            IEnumerable enumerable = (IEnumerable)valueToConvert;

                            foreach (object element in enumerable)
                            {
                                if (builder.Length > 0)
                                {
                                    builder.Append(';');
                                }

                                // we need to convert and escape each element of the array
                                builder.Append(EscapingUtilities.Escape(ConvertToString(element)));
                            }

                            convertedString = OpportunisticIntern.InternableToString(builder);
                        }
                    }
                    else
                    {
                        // The fall back is always to just convert to a string directly.
                        convertedString = valueToConvert.ToString();
                    }

                    return convertedString;
                }
                else
                {
                    return String.Empty;
                }
            }

            /// <summary>
            /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo)
            /// </summary>
            private static object LookupProperty(IPropertyProvider<T> properties, string propertyName, IElementLocation elementLocation, UsedUninitializedProperties usedUninitializedProperties)
            {
                return LookupProperty(properties, propertyName, 0, propertyName.Length - 1, elementLocation, usedUninitializedProperties);
            }

            /// <summary>
            /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo)
            /// </summary>
            private static object LookupProperty(IPropertyProvider<T> properties, string propertyName, int startIndex, int endIndex, IElementLocation elementLocation, UsedUninitializedProperties usedUninitializedProperties)
            {
                T property = properties.GetProperty(propertyName, startIndex, endIndex);

                object propertyValue;

                if (property == null && MSBuildNameIgnoreCaseComparer.Equals("MSBuild", propertyName, startIndex, 7))
                {
                    // It could be one of the MSBuildThisFileXXXX properties,
                    // whose values vary according to the file they are in.
                    if (startIndex != 0 || endIndex != propertyName.Length)
                    {
                        propertyValue = ExpandMSBuildThisFileProperty(propertyName.Substring(startIndex, endIndex - startIndex + 1), elementLocation);
                    }
                    else
                    {
                        propertyValue = ExpandMSBuildThisFileProperty(propertyName, elementLocation);
                    }
                }
                else if (property == null)
                {
                    // We have evaluated a property to null. We now need to see if we need to add it to the list of properties which are used before they have been initialized
                    // 
                    // We also do not want to add the property to the list if the environment variable is not set, also we do not want to add the property to the list if we are currently 
                    // evaluating a condition because a common pattern for msbuild projects is to see if the property evaluates to empty and then set a value as this would cause a considerable number of false positives.   <A Condition="'$(A)' == ''">default</A>
                    // 
                    // Another pattern used is where a property concatonates with other values,  <a>$(a);something</a> however we do not want to add the a element to the list because again this would make a number of 
                    // false positives. Therefore we check to see what element we are currently evaluating and if it is the same as our property we do not add the property to the list.
                    if (usedUninitializedProperties.Warn && usedUninitializedProperties.CurrentlyEvaluatingPropertyElementName != null)
                    {
                        // Check to see if the property name does not match the property we are currently evaluating, note the property we are currently evaluating in the element name, this means no $( or )
                        if (!MSBuildNameIgnoreCaseComparer.Equals(usedUninitializedProperties.CurrentlyEvaluatingPropertyElementName, propertyName, startIndex, endIndex - startIndex + 1))
                        {
                            string propertyTrimed = propertyName.Substring(startIndex, endIndex - startIndex + 1);
                            if (!usedUninitializedProperties.Properties.ContainsKey(propertyTrimed))
                            {
                                usedUninitializedProperties.Properties.Add(propertyTrimed, elementLocation);
                            }
                        }
                    }

                    propertyValue = String.Empty;
                }
                else
                {
                    propertyValue = property.EvaluatedValueEscaped;
                }

                return propertyValue;
            }

            /// <summary>
            /// If the property name provided is one of the special
            /// per file properties named "MSBuildThisFileXXXX" then returns the value of that property.
            /// If the location provided does not have a path (eg., if it comes from a file that has
            /// never been saved) then returns empty string.
            /// If the property name is not one of those properties, returns empty string.
            /// </summary>
            private static object ExpandMSBuildThisFileProperty(string propertyName, IElementLocation elementLocation)
            {
                if (!ReservedPropertyNames.IsReservedProperty(propertyName))
                {
                    return String.Empty;
                }

                if (elementLocation.File.Length == 0)
                {
                    return String.Empty;
                }

                string value = String.Empty;

                // Because String.Equals checks the length first, and these strings are almost
                // all different lengths, this sequence is efficient.
                if (String.Equals(propertyName, ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase))
                {
                    value = Path.GetFileName(elementLocation.File);
                }
                else if (String.Equals(propertyName, ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase))
                {
                    value = Path.GetFileNameWithoutExtension(elementLocation.File);
                }
                else if (String.Equals(propertyName, ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    value = FileUtilities.NormalizePath(elementLocation.File);
                }
                else if (String.Equals(propertyName, ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    value = Path.GetExtension(elementLocation.File);
                }
                else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    value = FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(elementLocation.File));
                }
                else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase))
                {
                    string directory = Path.GetDirectoryName(elementLocation.File);
                    int rootLength = Path.GetPathRoot(directory).Length;
                    string directoryNoRoot = directory.Substring(rootLength);
                    directoryNoRoot = FileUtilities.EnsureTrailingSlash(directoryNoRoot);
                    directoryNoRoot = FileUtilities.EnsureNoLeadingSlash(directoryNoRoot);
                    value = directoryNoRoot;
                }

                return value;
            }

            /// <summary>
            /// Given a string like "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation", return the value at that location
            /// in the registry. If the value isn't found, returns String.Empty.
            /// Properties may refer to a registry location by using the syntax for example
            /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
            /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
            /// the default value is desired.
            /// </summary>
            private static string ExpandRegistryValue(string registryExpression, IElementLocation elementLocation)
            {
                // Remove "Registry:" prefix
                string registryLocation = registryExpression.Substring(9);

                // Split off the value name -- the part after the "@" sign. If there's no "@" sign, then it's the default value name
                // we want.
                int firstAtSignOffset = registryLocation.IndexOf('@');
                int lastAtSignOffset = registryLocation.LastIndexOf('@');

                ProjectErrorUtilities.VerifyThrowInvalidProject(firstAtSignOffset == lastAtSignOffset, elementLocation, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", String.Empty);

                string valueName = lastAtSignOffset == -1 || lastAtSignOffset == registryLocation.Length - 1
                    ? null : registryLocation.Substring(lastAtSignOffset + 1);

                // If there's no '@', or '@' is first, then we'll use null or String.Empty for the location; otherwise
                // the location is the part before the '@'
                string registryKeyName = lastAtSignOffset != -1 ? registryLocation.Substring(0, lastAtSignOffset) : registryLocation;

                string result = String.Empty;
                if (registryKeyName != null)
                {
                    // We rely on the '@' character to delimit the key and its value, but the registry
                    // allows this character to be used in the names of keys and the names of values.
                    // Hence we use our standard escaping mechanism to allow users to access such keys
                    // and values.
                    registryKeyName = EscapingUtilities.UnescapeAll(registryKeyName);

                    if (valueName != null)
                    {
                        valueName = EscapingUtilities.UnescapeAll(valueName);
                    }

                    try
                    {
                        object valueFromRegistry = Registry.GetValue(registryKeyName, valueName, null /* default if key or value name is not found */);

                        if (null != valueFromRegistry)
                        {
                            // Convert the result to a string that is reasonable for MSBuild
                            result = ConvertToString(valueFromRegistry);
                        }
                        else
                        {
                            // This means either the key or value was not found in the registry.  In this case,
                            // we simply expand the property value to String.Empty to imitate the behavior of
                            // normal properties.
                            result = String.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionHandling.NotExpectedRegistryException(ex))
                        {
                            throw;
                        }

                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", ex.Message);
                    }
                }

                return result;
            }
        }

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
        ///     "my list: @(files->'')                              expands to string      "my list: ;"
        /// </summary>
        /// <remarks>
        /// This is a private nested class, exposed only through the Expander class.
        /// That allows it to hide its private methods even from Expander.
        /// </remarks>
        private static class ItemExpander
        {
            /// <summary>
            /// Execute the list of transform functions
            /// </summary>
            /// <typeparam name="S">class, IItem</typeparam>
            internal static IEnumerable<Tuple<string, S>> Transform<S>(Expander<P, I> expander, bool includeNullEntries, Stack<TransformFunction<S>> transformFunctionStack, IEnumerable<Tuple<string, S>> itemsOfType)
                where S : class, IItem
            {
                // If we have transforms on our stack, then we'll execute those first
                // This effectively runs backwards through the set
                if (transformFunctionStack.Count > 0)
                {
                    TransformFunction<S> function = transformFunctionStack.Pop();

                    foreach (Tuple<string, S> item in Transform(expander, includeNullEntries, transformFunctionStack, function.Execute(expander, includeNullEntries, itemsOfType)))
                    {
                        yield return item;
                    }
                }
                else
                {
                    // When we have no more tranforms on the stack, iterate over the items
                    // that we have to return them
                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        yield return item;
                    }
                }
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
            /// <typeparam name="S">Type of the items provided by the item source used for expansion</typeparam>
            /// <typeparam name="T">Type of the items that should be returned</typeparam>
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

                return ExpandExpressionCaptureIntoItems(expressionCapture, expander, items, itemFactory, options, includeNullEntries,
                    out isTransformExpression, elementLocation);
            }

            internal static ExpressionShredder.ItemExpressionCapture ExpandSingleItemVectorExpressionIntoExpressionCapture(
                    string expression, ExpanderOptions options, IElementLocation elementLocation)
            {
                if (((options & ExpanderOptions.ExpandItems) == 0) || (expression.Length == 0))
                {
                    return null;
                }

                List<ExpressionShredder.ItemExpressionCapture> matches = null;

                if (s_invariantCompareInfo.IndexOf(expression, '@') == -1)
                {
                    return null;
                }
                else
                {
                    matches = ExpressionShredder.GetReferencedItemExpressions(expression);

                    if (matches == null)
                    {
                        return null;
                    }
                }

                ExpressionShredder.ItemExpressionCapture match = matches[0];

                // We have a single valid @(itemlist) reference in the given expression.
                // If the passed-in expression contains exactly one item list reference,
                // with nothing else concatenated to the beginning or end, then proceed
                // with itemizing it, otherwise error.
                ProjectErrorUtilities.VerifyThrowInvalidProject(match.Value == expression, elementLocation, "EmbeddedItemVectorCannotBeItemized", expression);
                ErrorUtilities.VerifyThrow(matches.Count == 1, "Expected just one item vector");

                return match;
            }

            internal static IList<T> ExpandExpressionCaptureIntoItems<S, T>(
                    ExpressionShredder.ItemExpressionCapture expressionCapture, Expander<P, I> expander, IItemProvider<S> items, IItemFactory<S, T> itemFactory,
                    ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
                where S : class, IItem
                where T : class, IItem
            {
                ErrorUtilities.VerifyThrow(items != null, "Cannot expand items without providing items");

                isTransformExpression = false;

                // If the incoming factory doesn't have an item type that it can use to 
                // create items, it's our indication that the caller wants its items to have the type of the
                // expression being expanded. For example, items from expanding "@(Compile") should
                // have the item type "Compile".
                if (itemFactory.ItemType == null)
                {
                    itemFactory.ItemType = expressionCapture.ItemType;
                }

                IList<T> result = null;

                if (expressionCapture.Separator != null)
                {
                    // Reference contains a separator, for example @(Compile, ';').
                    // We need to flatten the list into 
                    // a scalar and then create a single item. Basically we need this
                    // to be able to convert item lists with user specified separators into properties.
                    string expandedItemVector;
                    using (var builder = new ReuseableStringBuilder())
                    {
                        bool brokeEarlyNonEmpty = ExpandItemVectorMatchIntoStringBuilder(expander, expressionCapture, items, elementLocation, builder, options);

                        if (brokeEarlyNonEmpty)
                        {
                            return null;
                        }

                        expandedItemVector = OpportunisticIntern.InternableToString(builder);
                    }

                    result = new List<T>(1);

                    if (expandedItemVector.Length > 0)
                    {
                        T newItem = itemFactory.CreateItem(expandedItemVector, elementLocation.File);

                        result.Add(newItem);
                    }

                    return result;
                }

                // No separator. Expand to a list of items
                // 
                // eg.,:
                // expands @(Compile) to a set of items cloned from the items in the "Compile" list
                // expands @(Compile->'%(foo)') to a set of items with the Include value of items in the "Compile" list.
                if (expressionCapture.Captures != null)
                {
                    isTransformExpression = true;
                }

                ICollection<S> itemsOfType = items.GetItems(expressionCapture.ItemType);

                // If there are no items of the given type, then bail out early
                if (itemsOfType.Count == 0)
                {
                    // .. but only if there isn't a function "Count()", since that will want to return something (zero) for an empty list
                    if (expressionCapture.Captures == null || !expressionCapture.Captures.Any(capture => String.Equals(capture.FunctionName, "Count", StringComparison.OrdinalIgnoreCase)))
                    {
                        return new List<T>();
                    }
                }

                result = new List<T>(itemsOfType.Count);

                if (!isTransformExpression)
                {
                    // No transform: expression is like @(Compile), so copy the items
                    foreach (S item in itemsOfType)
                    {
                        if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                        {
                            return null;
                        }

                        T newItem = itemFactory.CreateItem(item, elementLocation.File);

                        result.Add(newItem);
                    }

                    return result;
                }

                Stack<TransformFunction<S>> transformFunctionStack = PrepareTransformStackFromMatch<S>(elementLocation, expressionCapture);

                // iterate over our tranform chain, creating the final items from its results
                foreach (Tuple<string, S> itemTuple in Transform<S>(expander, includeNullEntries, transformFunctionStack, IntrinsicItemFunctions<S>.GetItemTupleEnumerator(itemsOfType)))
                {
                    if (itemTuple.Item1 != null && itemTuple.Item2 == null)
                    {
                        // We have an itemspec, but no base item
                        result.Add(itemFactory.CreateItem(itemTuple.Item1, elementLocation.File));
                    }
                    else if (itemTuple.Item1 != null && itemTuple.Item2 != null)
                    {
                        // We have both an itemspec, and a base item
                        result.Add(itemFactory.CreateItem(itemTuple.Item1, itemTuple.Item2, elementLocation.File));
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
            /// Expands all item vectors embedded in the given expression into a single string.
            /// If the expression is empty, returns empty string.
            /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
            /// </summary>
            /// <typeparam name="T">Type of the items provided</typeparam>
            internal static string ExpandItemVectorsIntoString<T>(Expander<P, I> expander, string expression, IItemProvider<T> items, ExpanderOptions options, IElementLocation elementLocation)
                where T : class, IItem
            {
                if (((options & ExpanderOptions.ExpandItems) == 0) || (expression.Length == 0))
                {
                    return expression;
                }

                ErrorUtilities.VerifyThrow(items != null, "Cannot expand items without providing items");

                List<ExpressionShredder.ItemExpressionCapture> matches = ExpressionShredder.GetReferencedItemExpressions(expression);

                if (matches == null)
                {
                    return expression;
                }

                using (var builder = new ReuseableStringBuilder())
                {
                    // As we walk through the matches, we need to copy out the original parts of the string which
                    // are not covered by the match.  This preserves original behavior which did not trim whitespace
                    // from between separators.
                    int lastStringIndex = 0;
                    for (int i = 0; i < matches.Count; i++)
                    {
                        if (matches[i].Index > lastStringIndex)
                        {
                            if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                            {
                                return null;
                            }

                            builder.Append(expression, lastStringIndex, matches[i].Index - lastStringIndex);
                        }

                        bool brokeEarlyNonEmpty = ExpandItemVectorMatchIntoStringBuilder(expander, matches[i], items, elementLocation, builder, options);

                        if (brokeEarlyNonEmpty)
                        {
                            return null;
                        }

                        lastStringIndex = matches[i].Index + matches[i].Length;
                    }

                    builder.Append(expression, lastStringIndex, expression.Length - lastStringIndex);

                    return OpportunisticIntern.InternableToString(builder);
                }
            }

            /// <summary>
            /// Prepare the stack of transforms that will be executed on a given set of items
            /// </summary>
            /// <typeparam name="S">class, IItem</typeparam>
            private static Stack<TransformFunction<S>> PrepareTransformStackFromMatch<S>(IElementLocation elementLocation, ExpressionShredder.ItemExpressionCapture match)
                where S : class, IItem
            {
                // There's something wrong with the expression, and we ended up with no function names
                ProjectErrorUtilities.VerifyThrowInvalidProject(match.Captures.Count > 0, elementLocation, "InvalidFunctionPropertyExpression");

                Stack<TransformFunction<S>> transformFunctionStack = new Stack<TransformFunction<S>>(match.Captures.Count);

                // Create a TransformFunction for each transform in the chain by extracting the relevant information
                // from the regex parsing results
                // Each will be pushed onto a stack in right to left order (i.e. the inner/right most will be on the
                // bottom of the stack, the outer/left most will be on the top
                for (int n = match.Captures.Count - 1; n >= 0; n--)
                {
                    string function = match.Captures[n].Value;
                    string functionName = match.Captures[n].FunctionName;
                    string argumentsExpression = match.Captures[n].FunctionArguments;

                    string[] arguments = null;

                    if (functionName == null)
                    {
                        functionName = "ExpandQuotedExpressionFunction";
                        arguments = new string[] { function };
                    }
                    else if (argumentsExpression != null)
                    {
                        arguments = ExtractFunctionArguments(elementLocation, argumentsExpression, argumentsExpression);
                    }

                    IntrinsicItemFunctions<S>.ItemTransformFunction transformFunction = IntrinsicItemFunctions<S>.GetItemTransformFunction(elementLocation, functionName, typeof(S));

                    // Push our tranform on to the stack
                    transformFunctionStack.Push(new TransformFunction<S>(elementLocation, functionName, transformFunction, arguments));
                }

                return transformFunctionStack;
            }

            /// <summary>
            /// Expand the match provided into a string, and append that to the provided string builder.
            /// Returns true if ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and so it broke out early.
            /// </summary>
            /// <typeparam name="S">Type of source items</typeparam>
            private static bool ExpandItemVectorMatchIntoStringBuilder<S>(Expander<P, I> expander, ExpressionShredder.ItemExpressionCapture match, IItemProvider<S> items, IElementLocation elementLocation, ReuseableStringBuilder builder, ExpanderOptions options)
                where S : class, IItem
            {
                string itemType = match.ItemType;
                string separator = (match.Separator != null) ? match.Separator : ";";

                // There's something wrong with the expression, and we ended up with a blank item type
                ProjectErrorUtilities.VerifyThrowInvalidProject(!String.IsNullOrEmpty(itemType), elementLocation, "InvalidFunctionPropertyExpression");

                ICollection<S> itemsOfType = items.GetItems(itemType);

                // If there are no items of the given type, then bail out early
                if (itemsOfType.Count == 0)
                {
                    // .. but only if there isn't a function "Count()", since that will want to return something (zero) for an empty list
                    if (match.Captures == null || !match.Captures.Any(capture => String.Equals(capture.FunctionName, "Count", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false; // false because the result is reliable
                    }
                }

                if (match.Captures == null)
                {
                    foreach (S item in itemsOfType)
                    {
                        // No transform: expression is like @(Compile), so append the item spec
                        if ((item.EvaluatedIncludeEscaped.Length > 0) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                        {
                            return true;
                        }

                        builder.Append(item.EvaluatedIncludeEscaped);
                        builder.Append(separator);
                    }
                }
                else
                {
                    Stack<TransformFunction<S>> transformFunctionStack = PrepareTransformStackFromMatch<S>(elementLocation, match);

                    // iterate over our tranform chain, creating the final items from its results
                    foreach (Tuple<string, S> itemTuple in Transform(expander, true /* including null items */, transformFunctionStack, IntrinsicItemFunctions<S>.GetItemTupleEnumerator(itemsOfType)))
                    {
                        if (itemTuple.Item1 != null && itemTuple.Item1.Length > 0 && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                        {
                            return true; // broke out early; result cannot be trusted
                        }
                        else if (itemTuple.Item1 != null)
                        {
                            builder.Append(itemTuple.Item1);
                        }

                        builder.Append(separator);
                    }
                }

                // Remove the extra separator
                if (builder.Length > 0)
                {
                    builder.Remove(builder.Length - separator.Length, separator.Length);
                }

                return false; // did not break early
            }

            /// <summary>
            /// The set of functions that called during an item transformation, e.g. @(CLCompile->ContainsMetadata('MetaName', 'metaValue'))
            /// </summary>
            /// <typeparam name="S">class, IItem</typeparam>
            internal static class IntrinsicItemFunctions<S>
                where S : class, IItem
            {
                /// <summary>
                /// A cache of previously created item function delegates
                /// </summary>
                private static ConcurrentDictionary<string, ItemTransformFunction> s_transformFunctionDelegateCache = new ConcurrentDictionary<string, ItemTransformFunction>(StringComparer.OrdinalIgnoreCase);

                /// <summary>
                /// Delegate that represents the signature of all item transformation functions
                /// This is used to support calling the functions by name
                /// </summary>
                public delegate IEnumerable<Tuple<string, S>> ItemTransformFunction(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments);

                /// <summary>
                /// Get a delegate to the given item transformation function by supplying the name and the
                /// Item type that should be used
                /// </summary>
                internal static ItemTransformFunction GetItemTransformFunction(IElementLocation elementLocation, string functionName, Type itemType)
                {
                    ItemTransformFunction transformFunction = null;
                    string qualifiedFunctionName = itemType.FullName + "::" + functionName;

                    // We may have seen this delegate before, if so grab the one we already created
                    if (!s_transformFunctionDelegateCache.TryGetValue(qualifiedFunctionName, out transformFunction))
                    {
                        if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
                        {
                            // Create a delegate to the function we're going to call
                            transformFunction = new ItemTransformFunction(ItemSpecModifierFunction);
                        }
                        else
                        {
                            MethodInfo itemFunctionInfo = typeof(IntrinsicItemFunctions<S>).GetMethod(functionName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Static);

                            if (itemFunctionInfo == null)
                            {
                                functionName = "ExecuteStringFunction";
                                itemFunctionInfo = typeof(IntrinsicItemFunctions<S>).GetMethod(functionName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Static);
                                if (itemFunctionInfo == null)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName);
                                    return null;
                                }

                                transformFunction = (ItemTransformFunction)Delegate.CreateDelegate(typeof(ItemTransformFunction), itemFunctionInfo, false);
                            }
                            else
                            {
                                // Create a delegate to the function we're going to call
                                transformFunction = (ItemTransformFunction)Delegate.CreateDelegate(typeof(ItemTransformFunction), itemFunctionInfo, false);
                            }
                        }

                        if (transformFunction == null)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName);
                            return null;
                        }

                        // record our delegate for future use
                        s_transformFunctionDelegateCache[qualifiedFunctionName] = transformFunction;
                    }

                    return transformFunction;
                }

                /// <summary>
                /// Create an enumerator from a base IEnumerable of items into an enumerable
                /// of transformation result which includes the new itemspec and the base item
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> GetItemTupleEnumerator(IEnumerable<S> itemsOfType)
                {
                    // iterate over the items, and yield out items in the tuple format
                    foreach (S item in itemsOfType)
                    {
                        yield return new Tuple<string, S>(item.EvaluatedIncludeEscaped, item);
                    }
                }

                /// <summary>
                /// Intrinsic function that returns the number of items in the list
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> Count(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    yield return new Tuple<string, S>(Convert.ToString(itemsOfType.Count(), CultureInfo.InvariantCulture), null /* no base item */);
                }

                /// <summary>
                /// Intrinsic function that returns the specified built-in modifer value of the items in itemsOfType
                /// Tuple is {current item include, item under transformation}
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> ItemSpecModifierFunction(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        // If the item include has become empty,
                        // this is the end of the pipeline for this item
                        if (String.IsNullOrEmpty(item.Item1))
                        {
                            continue;
                        }

                        string result = null;

                        try
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case, we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            string directoryToUse = item.Item2.ProjectDirectory ?? Directory.GetCurrentDirectory();
                            string definingProjectEscaped = item.Item2.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);

                            result = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(directoryToUse, item.Item1, definingProjectEscaped, functionName);
                        }
                        catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
                        {
                            // InvalidOperationException is how GetItemSpecModifier communicates invalid conditions upwards, so 
                            // we do not want to rethrow in that case.  
                            if (ExceptionHandling.NotExpectedException(e) && !(e is InvalidOperationException))
                            {
                                throw;
                            }

                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Item1, e.Message);
                        }

                        if (!String.IsNullOrEmpty(result))
                        {
                            // GetItemSpecModifier will have returned us an escaped string
                            // there is nothing more to do than yield it into the pipeline
                            yield return new Tuple<string, S>(result, item.Item2);
                        }
                        else if (includeNullEntries)
                        {
                            yield return new Tuple<string, S>(null, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns the DirectoryName of the items in itemsOfType
                /// UNDONE: This can be removed in favor of a built-in %(DirectoryName) metadata in future.
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> DirectoryName(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    Dictionary<string, string> directoryNameTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        // If the item include has become empty,
                        // this is the end of the pipeline for this item
                        if (String.IsNullOrEmpty(item.Item1))
                        {
                            continue;
                        }

                        string directoryName = null;

                        if (!directoryNameTable.TryGetValue(item.Item1, out directoryName))
                        {
                            // Unescape as we are passing to the file system
                            string unescapedPath = EscapingUtilities.UnescapeAll(item.Item1);

                            try
                            {
                                string rootedPath;

                                // If we're a projectitem instance then we need to get
                                // the project directory and be relative to that
                                if (Path.IsPathRooted(unescapedPath))
                                {
                                    rootedPath = unescapedPath;
                                }
                                else
                                {
                                    // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                                    // In that case, we're safe to get the current directory as we'll be running on TaskItems which
                                    // only exist within a target where we can trust the current directory
                                    string baseDirectoryToUse = item.Item2.ProjectDirectory ?? String.Empty;
                                    rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                                }

                                directoryName = Path.GetDirectoryName(rootedPath);
                            }
                            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Item1, e.Message);
                            }

                            // Escape as this is going back into the engine
                            directoryName = EscapingUtilities.Escape(directoryName);
                            directoryNameTable[unescapedPath] = directoryName;
                        }

                        if (!String.IsNullOrEmpty(directoryName))
                        {
                            // return a result through the enumerator
                            yield return new Tuple<string, S>(directoryName, item.Item2);
                        }
                        else if (includeNullEntries)
                        {
                            yield return new Tuple<string, S>(null, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns the contents of the metadata in specified in argument[0]
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> Metadata(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments != null && arguments.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    string metadataName = arguments[0];

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        if (item.Item2 != null)
                        {
                            string metadataValue = null;

                            try
                            {
                                metadataValue = item.Item2.GetMetadataValueEscaped(metadataName);
                            }
                            catch (ArgumentException ex) // Blank metadata name
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }
                            catch (InvalidOperationException ex)
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }

                            if (!String.IsNullOrEmpty(metadataValue))
                            {
                                // It may be that the itemspec has unescaped ';'s in it so we need to split here to handle
                                // that case.
                                if (s_invariantCompareInfo.IndexOf(metadataValue, ';') >= 0)
                                {
                                    IList<string> splits = ExpressionShredder.SplitSemiColonSeparatedList(metadataValue);

                                    foreach (string itemSpec in splits)
                                    {
                                        // return a result through the enumerator
                                        yield return new Tuple<string, S>(itemSpec, item.Item2);
                                    }
                                }
                                else
                                {
                                    // return a result through the enumerator
                                    yield return new Tuple<string, S>(metadataValue, item.Item2);
                                }
                            }
                            else if (metadataValue != String.Empty && includeNullEntries)
                            {
                                yield return new Tuple<string, S>(metadataValue, item.Item2);
                            }
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case sensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> DistinctWithCase(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    return DistinctWithComparer(expander, elementLocation, includeNullEntries, functionName, itemsOfType, arguments, StringComparer.Ordinal);
                }

                /// <summary>
                /// Intrinsic function that returns only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case insensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> Distinct(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    return DistinctWithComparer(expander, elementLocation, includeNullEntries, functionName, itemsOfType, arguments, StringComparer.OrdinalIgnoreCase);
                }

                /// <summary>
                /// Intrinsic function that returns only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case insensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> DistinctWithComparer(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments, StringComparer comparer)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    // This dictionary will ensure that we only return one result per unique itemspec
                    Dictionary<string, S> seenItems = new Dictionary<string, S>(comparer);

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        if (item.Item1 != null && !seenItems.ContainsKey(item.Item1))
                        {
                            seenItems[item.Item1] = item.Item2;

                            yield return new Tuple<string, S>(item.Item1, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function reverses the item list.
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> Reverse(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));
                    foreach (Tuple<String, S> item in itemsOfType.Reverse())
                    {
                        yield return new Tuple<string, S>(item.Item1, item.Item2);
                    }
                }

                /// <summary>
                /// Intrinsic function that transforms expressions like the %(foo) in @(Compile->'%(foo)')
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> ExpandQuotedExpressionFunction(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments != null && arguments.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        MetadataMatchEvaluator matchEvaluator;
                        string include = null;

                        // If we've been handed a null entry by an uptream tranform
                        // then we don't want to try to tranform it with an itempec modification.
                        // Simply allow the null to be passed along (if, we are including nulls as specified by includeNullEntries
                        if (item.Item1 != null)
                        {
                            matchEvaluator = new MetadataMatchEvaluator(item.Item1, item.Item2, elementLocation);

                            include = RegularExpressions.ItemMetadataPattern.Replace(arguments[0], matchEvaluator.GetMetadataValueFromMatch);
                        }

                        // Include may be empty. Historically we have created items with empty include
                        // and ultimately set them on tasks, but we don't do that anymore as it's broken.
                        // Instead we optionally add a null, so that input and output lists are the same length; this allows
                        // the caller to possibly do correlation.

                        // We pass in the existing item so we can copy over its metadata
                        if (include != null && include.Length > 0)
                        {
                            yield return new Tuple<string, S>(include, item.Item2);
                        }
                        else if (includeNullEntries)
                        {
                            yield return new Tuple<string, S>(null, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that transforms expressions by invoking methods of System.String on the itemspec
                /// of the item in the pipeline
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> ExecuteStringFunction(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    // Transform: expression is like @(Compile->'%(foo)'), so create completely new items,
                    // using the Include from the source items
                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        Function<P> function = new Expander<P, I>.Function<P>(typeof(string), item.Item1, item.Item1, functionName, arguments, BindingFlags.Public | BindingFlags.InvokeMethod, String.Empty, expander.UsedUninitializedProperties);

                        object result = function.Execute(item.Item1, expander._properties, ExpanderOptions.ExpandAll, elementLocation);

                        string include = Expander<P, I>.PropertyExpander<P>.ConvertToString(result);

                        // We pass in the existing item so we can copy over its metadata
                        if (include.Length > 0)
                        {
                            yield return new Tuple<string, S>(include, item.Item2);
                        }
                        else if (includeNullEntries)
                        {
                            yield return new Tuple<string, S>(null, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns the items from itemsOfType with their metadata cleared, i.e. only the itemspec is retained
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> ClearMetadata(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        if (includeNullEntries || item.Item1 != null)
                        {
                            yield return new Tuple<string, S>(item.Item1, null);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns only those items that have a not-blank value for the metadata specified
                /// Using a case insensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> HasMetadata(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments != null && arguments.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    string metadataName = arguments[0];

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item2.GetMetadataValueEscaped(metadataName);
                        }
                        catch (ArgumentException ex) // Blank metadata name
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }
                        catch (InvalidOperationException ex)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        // GetMetadataValueEscaped returns empty string for missing metadata,
                        // but IItem specifies it should return null
                        if (metadataValue != null && metadataValue.Length > 0)
                        {
                            // return a result through the enumerator
                            yield return new Tuple<string, S>(item.Item1, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns only those items have the given metadata value
                /// Using a case insensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> WithMetadataValue(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments != null && arguments.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    string metadataName = arguments[0];
                    string metadataValueToFind = arguments[1];

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item2.GetMetadataValueEscaped(metadataName);
                        }
                        catch (ArgumentException ex) // Blank metadata name
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }
                        catch (InvalidOperationException ex)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            // return a result through the enumerator
                            yield return new Tuple<string, S>(item.Item1, item.Item2);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that returns a boolean to indicate if any of the items have the given metadata value
                /// Using a case insensitive comparison
                /// </summary>
                internal static IEnumerable<Tuple<string, S>> AnyHaveMetadataValue(Expander<P, I> expander, IElementLocation elementLocation, bool includeNullEntries, string functionName, IEnumerable<Tuple<string, S>> itemsOfType, string[] arguments)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments != null && arguments.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, (arguments == null ? 0 : arguments.Length));

                    string metadataName = arguments[0];
                    string metadataValueToFind = arguments[1];
                    bool metadataFound = false;

                    foreach (Tuple<string, S> item in itemsOfType)
                    {
                        if (item.Item2 != null)
                        {
                            string metadataValue = null;

                            try
                            {
                                metadataValue = item.Item2.GetMetadataValueEscaped(metadataName);
                            }
                            catch (ArgumentException ex) // Blank metadata name
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }
                            catch (InvalidOperationException ex)
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }

                            if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                            {
                                metadataFound = true;

                                // return a result through the enumerator
                                yield return new Tuple<string, S>("true", item.Item2);

                                // break out as soon as we found a match
                                yield break;
                            }
                        }
                    }

                    if (!metadataFound)
                    {
                        // We did not locate an item with the required metadata
                        yield return new Tuple<string, S>("false", null);
                    }
                }
            }

            /// <summary>
            /// Represents all the components of a transform function, including the ability to execute it 
            /// </summary>
            /// <typeparam name="S">class, IItem</typeparam>
            internal class TransformFunction<S>
                where S : class, IItem
            {
                /// <summary>
                /// The delegate that points to the transform function
                /// </summary>
                private IntrinsicItemFunctions<S>.ItemTransformFunction _transform;

                /// <summary>
                /// Arguments to pass to the transform function as parsed out of the project file
                /// </summary>
                private string[] _arguments;

                /// <summary>
                /// The element location of the transform expression
                /// </summary>
                private IElementLocation _elementLocation;

                /// <summary>
                /// The name of the function that this class will call
                /// </summary>
                private string _functionName;

                /// <summary>
                /// TransformFunction constructor
                /// </summary>
                public TransformFunction(IElementLocation elementLocation, string functionName, IntrinsicItemFunctions<S>.ItemTransformFunction transform, string[] arguments)
                {
                    _elementLocation = elementLocation;
                    _functionName = functionName;
                    _transform = transform;
                    _arguments = arguments;
                }

                /// <summary>
                /// Arguments to pass to the transform function as parsed out of the project file
                /// </summary>
                public string[] Arguments
                {
                    get { return _arguments; }
                }

                /// <summary>
                /// The element location of the transform expression
                /// </summary>
                public IElementLocation ElementLocation
                {
                    get { return _elementLocation; }
                }

                /// <summary>
                /// Execute this transform function with the arguments contained within this TransformFunction instance
                /// </summary>
                public IEnumerable<Tuple<string, S>> Execute(Expander<P, I> expander, bool includeNullEntries, IEnumerable<Tuple<string, S>> itemsOfType)
                {
                    // Execute via the delegate
                    return _transform(expander, _elementLocation, includeNullEntries, _functionName, itemsOfType, _arguments);
                }
            }

            /// <summary>
            /// A functor that returns the value of the metadata in the match
            /// that is on the item it was created with.
            /// </summary>
            private class MetadataMatchEvaluator
            {
                /// <summary>
                /// The current ItemSpec of the item being matched
                /// </summary>
                private string _itemSpec;

                /// <summary>
                /// Item used as the source of metadata
                /// </summary>
                private IItem _sourceOfMetadata;

                /// <summary>
                /// Location of the match
                /// </summary>
                private IElementLocation _elementLocation;

                /// <summary>
                /// Constructor
                /// </summary>
                internal MetadataMatchEvaluator(string itemSpec, IItem sourceOfMetadata, IElementLocation elementLocation)
                {
                    _itemSpec = itemSpec;
                    _sourceOfMetadata = sourceOfMetadata;
                    _elementLocation = elementLocation;
                }

                /// <summary>
                /// Expands the metadata in the match provided into a string result.
                /// The match is expected to be the content of a transform.
                /// For example, representing "%(Filename.obj)" in the original expression "@(Compile->'%(Filename.obj)')"
                /// </summary>
                internal string GetMetadataValueFromMatch(Match match)
                {
                    string name = match.Groups[RegularExpressions.NameGroup].Value;

                    ProjectErrorUtilities.VerifyThrowInvalidProject(match.Groups[RegularExpressions.ItemSpecificationGroup].Length == 0, _elementLocation, "QualifiedMetadataInTransformNotAllowed", match.Value, name);

                    string value = null;
                    try
                    {
                        if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(name))
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case, we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            string directoryToUse = _sourceOfMetadata.ProjectDirectory ?? Directory.GetCurrentDirectory();
                            string definingProjectEscaped = _sourceOfMetadata.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);

                            value = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(directoryToUse, _itemSpec, definingProjectEscaped, name);
                        }
                        else
                        {
                            value = _sourceOfMetadata.GetMetadataValueEscaped(name);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "CannotEvaluateItemMetadata", name, ex.Message);
                    }

                    return value;
                }
            }
        }

        /// <summary>
        /// Regular expressions used by the expander.
        /// The expander currently uses regular expressions rather than a parser to do its work.
        /// </summary>
        private static class RegularExpressions
        {
            /**************************************************************************************************************************
            * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
            * description of an item vector changes, the expressions must be updated in both places.
            *************************************************************************************************************************/

            /// <summary>
            /// Regular expression used to match item metadata references embedded in strings.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary> 
            internal static readonly Regex ItemMetadataPattern = new Regex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

            /// <summary>
            /// Name of the group matching the "name" of a metadatum.
            /// </summary>
            internal const string NameGroup = "NAME";

            /// <summary>
            /// Name of the group matching the prefix on a metadata expression, for example "Compile." in "%(Compile.Object)"
            /// </summary>
            internal const string ItemSpecificationGroup = "ITEM_SPECIFICATION";

            /// <summary>
            /// Name of the group matching the item type in an item expression or metadata expression.
            /// </summary>
            internal const string ItemTypeGroup = "ITEM_TYPE";

            /// <summary>
            /// regular expression used to match item metadata references outside of item vector transforms
            /// </summary>
            /// <remarks>PERF WARNING: this Regex is complex and tends to run slowly</remarks>
            internal static readonly Regex NonTransformItemMetadataPattern =
                new Regex
                    (
                    @"((?<=" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" + ItemVectorWithTransformRHS + @")) | ((?<!" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?=" + ItemVectorWithTransformRHS + @")) | ((?<!" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" + ItemVectorWithTransformRHS + @"))",
                    RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture
                    );

            /// <summary>
            /// Complete description of an item metadata reference, including the optional qualifying item type.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary> 
            private const string ItemMetadataSpecification = @"%\(\s* (?<ITEM_SPECIFICATION>(?<ITEM_TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)? (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @") \s*\)";

            /// <summary>
            /// description of an item vector with a transform, left hand side 
            /// </summary> 
            private const string ItemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";

            /// <summary>
            /// description of an item vector with a transform, right hand side 
            /// </summary> 
            private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

            /**************************************************************************************************************************
             * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
             *************************************************************************************************************************/
        }

        /// <summary>
        /// This class represents the function as extracted from an expression
        /// It is also responsible for executing the function
        /// </summary>
        /// <typeparam name="T">Type of the properties used to expand the expression</typeparam>
        private class Function<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type that this function will act on
            /// </summary>
            private Type _objectType;

            /// <summary>
            /// The name of the function
            /// </summary>
            private string _name;

            /// <summary>
            /// The arguments for the function
            /// </summary>
            private string[] _arguments;

            /// <summary>
            /// The expression that constitutes this function
            /// </summary>
            private string _expression;

            /// <summary>
            /// The property name that is the context for this function
            /// </summary>
            private string _expressionRootName;

            /// <summary>
            /// The binding flags that will be used during invocation of this function
            /// </summary>
            private BindingFlags _bindingFlags;

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted
            /// </summary>
            private string _remainder;

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            private UsedUninitializedProperties _usedUninitializedProperties;

            /// <summary>
            /// Construct a function that will be executed during property evaluation
            /// </summary>
            internal Function(Type objectType, string expression, string expressionRootName, string name, string[] arguments, BindingFlags bindingFlags, string remainder, UsedUninitializedProperties usedUninitializedProperties)
            {
                _name = name;
                if (arguments == null)
                {
                    _arguments = new string[0];
                }
                else
                {
                    _arguments = arguments;
                }

                _expressionRootName = expressionRootName;
                _expression = expression;
                _objectType = objectType;
                _bindingFlags = bindingFlags;
                _remainder = remainder;
                _usedUninitializedProperties = usedUninitializedProperties;
            }

            /// <summary>
            /// Part of the extraction may result in the name of the property
            /// This accessor is used by the Expander
            /// Examples of expression root:
            ///     [System.Diagnostics.Process]::Start
            ///     SomeMSBuildProperty
            /// </summary>
            internal string ExpressionRootName
            {
                get { return _expressionRootName; }
            }

            /// <summary>
            /// Extract the function details from the given property function expression
            /// </summary>
            internal static Function<T> ExtractPropertyFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, UsedUninitializedProperties usedUnInitializedProperties)
            {
                // If this a expression function rather than a static, then we'll capture the name of the property referenced
                string propertyName = null;

                // The type of the object that this function is part
                Type objectType = null;

                // By default the expression root is the whole function expression
                string expressionRoot = expressionFunction;

                // The arguments for this function start at the first '('
                // If there are no arguments, then we're a property getter
                int argumentStartIndex = expressionFunction.IndexOf('(');

                // If we have arguments, then we only want the content up to but not including the '('
                if (argumentStartIndex > -1)
                {
                    expressionRoot = expressionFunction.Substring(0, argumentStartIndex);
                }

                // In case we ended up with something we don't understand
                ProjectErrorUtilities.VerifyThrowInvalidProject(!String.IsNullOrEmpty(expressionRoot), elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                // First we'll see if there is a static function being called
                // A static method is the content that follows the last "::", the rest being
                // the type
                int methodStartIndex = -1;

                // This is a static method call
                if (propertyValue == null && expressionRoot[0] == '[')
                {
                    int typeEndIndex = expressionRoot.IndexOf(']', 1);

                    if (typeEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                    }

                    string typeName = expressionRoot.Substring(1, typeEndIndex - 1);
                    methodStartIndex = typeEndIndex + 1;

                    // Make an attempt to locate a type that matches the body of the expression.
                    // We won't throw on error here
                    objectType = GetTypeForStaticMethod(typeName);

                    if (objectType == null)
                    {
                        // We ended up with something other than a type
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionTypeUnavailable", expressionFunction, typeName);
                    }

                    if (expressionRoot.Length > methodStartIndex + 2 && expressionRoot[methodStartIndex] == ':' && expressionRoot[methodStartIndex + 1] == ':')
                    {
                        // skip over the "::"
                        methodStartIndex += 2;
                    }
                    else
                    {
                        // We ended up with something other than a static function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                    }
                }
                else if (expressionFunction[0] == '[') // We have an indexer
                {
                    objectType = propertyValue.GetType();
                    int indexerEndIndex = expressionFunction.IndexOf(']', 1);

                    if (indexerEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }

                    string argumentsContent = expressionFunction.Substring(1, indexerEndIndex - 1);
                    methodStartIndex = indexerEndIndex + 1;
                    Function<T> indexerFunction = ConstructIndexerFunction(expressionFunction, elementLocation, propertyValue, propertyName, objectType, methodStartIndex, argumentsContent, usedUnInitializedProperties);

                    return indexerFunction;
                }
                else
                {
                    // No static function call was found, look for an instance function call next, such as in SomeStuff.ToLower()
                    methodStartIndex = expressionRoot.IndexOf('.');
                    if (methodStartIndex == -1)
                    {
                        // We don't have a function invocation in the expression root, return null
                        return null;
                    }
                    else
                    {
                        // skip over the '.';
                        methodStartIndex++;
                    }
                }

                // No type matched, therefore the content must be a property reference, or a recursive call as functions
                // are chained together
                if (objectType == null)
                {
                    int rootEndIndex = expressionRoot.IndexOf('.');
                    propertyName = expressionRoot.Substring(0, rootEndIndex);

                    // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                    if (propertyValue == null && !IsValidPropertyName(propertyName))
                    {
                        // We extracted something that wasn't a valid property name, fail.
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    }

                    objectType = typeof(string);
                }

                // If we are recursively acting on a type that has been already produced
                // then pass that type inwards
                if (propertyValue != null)
                {
                    objectType = propertyValue.GetType();
                }

                Function<T> function = ConstructFunction(elementLocation, expressionFunction, propertyName, objectType, argumentStartIndex, methodStartIndex, usedUnInitializedProperties);

                return function;
            }

            /// <summary>
            /// Execute the function on the given instance
            /// </summary>
            internal object Execute(object objectInstance, IPropertyProvider<T> properties, ExpanderOptions options, IElementLocation elementLocation)
            {
                object functionResult = String.Empty;
                object[] args = null;

                try
                {
                    // If there is no object instance, then the method invocation will be a static
                    if (objectInstance == null)
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsStaticMethodAvailable(_objectType, _name))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _name, _objectType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Static;

                        // For our intrinsic function we need to support calling of internal methods
                        // since we don't want them to be public
                        if (_objectType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                        {
                            _bindingFlags |= BindingFlags.NonPublic;
                        }
                    }
                    else
                    {
                        _bindingFlags |= BindingFlags.Instance;

                        // The object that we're about to call methods on may have escaped characters
                        // in it, we want to operate on the unescaped string in the function, just as we
                        // want to pass arguments that are unescaped (see below)
                        if (objectInstance is string)
                        {
                            objectInstance = EscapingUtilities.UnescapeAll((string)objectInstance);
                        }
                    }

                    // We have a methodinfo match, need to plug in the arguments
                    args = new object[_arguments.Length];

                    // Assemble our arguments ready for passing to our method
                    for (int n = 0; n < _arguments.Length; n++)
                    {
                        object argument = PropertyExpander<T>.ExpandPropertiesLeaveTypedAndEscaped(_arguments[n], properties, options, elementLocation, _usedUninitializedProperties);
                        string argumentValue = argument as string;

                        if (argumentValue != null)
                        {
                            // Unescape the value since we're about to send it out of the engine and into
                            // the function being called
                            args[n] = EscapingUtilities.UnescapeAll(argumentValue);
                        }
                        else
                        {
                            args[n] = argument;
                        }
                    }

                    // Handle special cases where the object type needs to affect the choice of method
                    // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and 
                    // fails the comparison, because what we have on the right is generally a string.
                    // This special casing is to realize that its a comparison that is taking place and handle the
                    // argument type coercion accordingly; effectively pre-preparing the argument type so 
                    // that it matches the left hand side ready for the default binder’s method invoke.
                    if (objectInstance != null && args.Length == 1 && (String.Equals("Equals", _name, StringComparison.OrdinalIgnoreCase) || String.Equals("CompareTo", _name, StringComparison.OrdinalIgnoreCase)))
                    {
                        // change the type of the final unescaped string into the destination
                        args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                    }

                    // If we've been asked for and instance to be constructed, then we
                    // need to locate an appropriate constructor and invoke it
                    if (String.Equals("new", _name, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                    }
                    else
                    {
                        // Execute the function given converted arguments
                        // The only exception that we should catch to try a late bind here is missing method
                        // otherwise there is the potential of running a function twice!
                        try
                        {
                            // First use InvokeMember using the standard binder - this will match and coerce as needed
                            functionResult = _objectType.InvokeMember(_name, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture);
                        }
                        catch (MissingMethodException ex) // Don't catch and retry on any other exception
                        {
                            // If we're invoking a method, then there are deeper attempts that
                            // can be made to invoke the method
                            if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                            {
                                // The standard binder failed, so do our best to coerce types into the arguments for the function
                                // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true. 
                                functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, false /* is not constructor */);
                            }
                            else
                            {
                                // We were asked to get a property or field, and we found that we cannot
                                // locate it. Since there is no further argument coersion possible
                                // we'll throw right now.
                                throw;
                            }
                        }
                    }

                    // If the result of the function call is a string, then we need to escape the result
                    // so that we maintain the "engine contains escaped data" state.
                    // The exception is that the user is explicitly calling MSBuild::Unescape or MSBuild::Escape
                    if (functionResult is string && !String.Equals("Unescape", _name, StringComparison.OrdinalIgnoreCase) && !String.Equals("Escape", _name, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = EscapingUtilities.Escape((string)functionResult);
                    }

                    // We have nothing left to parse, so we'll return what we have
                    if (String.IsNullOrEmpty(_remainder))
                    {
                        return functionResult;
                    }

                    // Recursively expand the remaining property body after execution
                    return PropertyExpander<T>.ExpandPropertyBody(_remainder, functionResult, properties, options, elementLocation, _usedUninitializedProperties);
                }

                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
                catch (TargetInvocationException ex)
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _name, args);
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                    return null;
                }

                // Any other exception was thrown by trying to call it
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedFunctionException(ex))
                    {
                        throw;
                    }

                    // If there's a :: in the expression, they were probably trying for a static function
                    // invocation. Give them some more relevant info in that case
                    if (s_invariantCompareInfo.IndexOf(_expression, "::", CompareOptions.OrdinalIgnoreCase) > -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", _expression, ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                    }
                    else
                    {
                        // We ended up with something other than a function expression
                        string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _name, args);
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                    }

                    return null;
                }
            }

            /// <summary>
            /// Return a Type object for the type we're trying to call static methods on
            /// </summary>
            private static Type GetTypeForStaticMethod(string typeName)
            {
                // Ultimately this will be a more in-depth lookup, including assembly name etc.
                // for now, we're only supporting a subset of what's in mscorlib + specific additional types
                // If the env var MSBUILDENABLEALLPROPERTYFUNCTIONS=1 then we'll allow pretty much anything
                Type objectType;
                Tuple<string, Type> functionType;

                // If we don't have a type name, we already know that we won't be able to find a type.  
                // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
                if (String.IsNullOrEmpty(typeName))
                {
                    return null;
                }

                // For whole types we support them being in different assemblies than mscorlib
                // Get the assembly qualified type name if one exists
                if (AvailableStaticMethods.TryGetValue(typeName, out functionType) && functionType != null)
                {
                    // We need at least one of these set
                    ErrorUtilities.VerifyThrow(functionType.Item1 != null || functionType.Item2 != null, "Function type information needs either string or type represented.");

                    // If we have the type information in Type form, then just return that
                    if (functionType.Item2 != null)
                    {
                        return functionType.Item2;
                    }
                    else if (functionType.Item1 != null)
                    {
                        // This is a case where the Type is not available at compile time, so
                        // we are forced to bind by name instead
                        typeName = functionType.Item1;

                        // Get the type from the assembly qualified type name from AvailableStaticMethods
                        objectType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                        // If we've used it once, chances are that we'll be using it again
                        // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                        AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, objectType));

                        return objectType;
                    }
                }

                // Get the type from mscorlib (or the currently running assembly)
                objectType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                if (objectType != null)
                {
                    // DO NOT CACHE THE TYPE HERE!
                    // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
                    // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                    return objectType;
                }

                // Note the following code path is only entered when MSBUILDENABLEALLPROPERTYFUNCTIONS == 1.
                // This environment variable must not be cached - it should be dynamically settable while the application is executing.
                if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                {
                    // We didn't find the type, so go probing. First in System
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssembly(typeName, "System");
                    }

                    // Next in System.Core
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssembly(typeName, "System.Core");
                    }

                    // We didn't find the type, so try to find it using the namespace
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssemblyUsingNamespace(typeName);
                    }

                    if (objectType != null)
                    {
                        // If we've used it once, chances are that we'll be using it again
                        // We can cache the type here, since all functions are enabled
                        AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, objectType));
                    }
                }

                return objectType;
            }

            /// <summary>
            /// Gets the specified type using the namespace to guess the assembly that its in
            /// </summary>
            private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
            {
                string baseName = typeName;
                int assemblyNameEnd = baseName.LastIndexOf('.');
                Type foundType = null;

                // If the string has no dot, or is nothing but a dot, we have no
                // namespace to look for, so we can't help.
                if (assemblyNameEnd <= 0)
                {
                    return null;
                }

                // We will work our way up the namespace looking for an assembly that matches
                while (assemblyNameEnd > 0)
                {
                    string candidateAssemblyName = null;

                    candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                    // Try to load the assembly with the computed name
                    foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

                    if (foundType != null)
                    {
                        // We have a match, so get the type from that assembly
                        return foundType;
                    }
                    else
                    {
                        // Keep looking as we haven't found a match yet
                        baseName = candidateAssemblyName;
                        assemblyNameEnd = baseName.LastIndexOf('.');
                    }
                }

                // We didn't find it, so we need to give up
                return null;
            }

            /// <summary>
            /// Get the specified type from the assembly partial name supplied
            /// </summary>
            [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
            private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
            {
                Type objectType = null;

                // Try to load the assembly with the computed name
#pragma warning disable 618
                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                // Assembly.Load requires the full assembly name to be passed to it.
                // Therefore we must ignore the deprecated warning.
                Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618

                if (candidateAssembly != null)
                {
                    objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
                }

                return objectType;
            }

            /// <summary>
            /// Factory method to construct an indexer function for property evaluation
            /// </summary>
            private static Function<T> ConstructIndexerFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, string propertyName, Type objectType, int methodStartIndex, string argumentsContent, UsedUninitializedProperties usedUnInitializedProperties)
            {
                string remainder = expressionFunction.Substring(methodStartIndex);
                string functionToInvoke;
                string[] functionArguments;

                // If there are no arguments, then just create an empty array
                if (String.IsNullOrEmpty(argumentsContent))
                {
                    functionArguments = new string[0];
                }
                else
                {
                    // We will keep empty entries so that we can treat them as null
                    functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                }

                // choose the name of the function based on the type of the object that we
                // are using.
                if (propertyValue is Array)
                {
                    functionToInvoke = "GetValue";
                }
                else if (propertyValue is string)
                {
                    functionToInvoke = "get_Chars";
                }
                else // a regular indexer
                {
                    functionToInvoke = "get_Item";
                }

                Function<T> indexerFunction;

                indexerFunction = new Function<T>(objectType, expressionFunction, propertyName, functionToInvoke, functionArguments, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod, remainder, usedUnInitializedProperties);
                return indexerFunction;
            }

            /// <summary>
            /// Factory method to construct a function for property evaluation
            /// </summary>
            private static Function<T> ConstructFunction(IElementLocation elementLocation, string expressionFunction, string expressionRootName, Type objectType, int argumentStartIndex, int methodStartIndex, UsedUninitializedProperties usedUninitializedProperties)
            {
                // The unevaluated and unexpanded arguments for this function
                string[] functionArguments;

                // The name of the function that will be invoked
                string functionToInvoke;

                // What's left of the expression once the function has been constructed
                string remainder = String.Empty;

                // The binding flags that we will use for this function's execution
                BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

                // There are arguments that need to be passed to the function
                if (argumentStartIndex > -1 && !expressionFunction.Substring(methodStartIndex, argumentStartIndex - methodStartIndex).Contains("."))
                {
                    string argumentsContent;

                    // separate the function and the arguments
                    functionToInvoke = expressionFunction.Substring(methodStartIndex, argumentStartIndex - methodStartIndex).Trim();

                    // Skip the '('
                    argumentStartIndex++;

                    // Scan for the matching closing bracket, skipping any nested ones
                    int argumentsEndIndex = ScanForClosingParenthesis(expressionFunction, argumentStartIndex);

                    if (argumentsEndIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                    }

                    // We have been asked for a method invocation
                    defaultBindingFlags |= BindingFlags.InvokeMethod;

                    // It may be that there are '()' but no actual arguments content
                    if (argumentStartIndex == expressionFunction.Length - 1)
                    {
                        argumentsContent = String.Empty;
                        functionArguments = new string[0];
                    }
                    else
                    {
                        // we have content within the '()' so let's extract and deal with it
                        argumentsContent = expressionFunction.Substring(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                        // If there are no arguments, then just create an empty array
                        if (String.IsNullOrEmpty(argumentsContent))
                        {
                            functionArguments = new string[0];
                        }
                        else
                        {
                            // We will keep empty entries so that we can treat them as null
                            functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                        }

                        remainder = expressionFunction.Substring(argumentsEndIndex + 1);
                    }
                }
                else
                {
                    int nextMethodIndex = expressionFunction.IndexOf('.', methodStartIndex);
                    int methodLength = expressionFunction.Length - methodStartIndex;
                    int indexerIndex = expressionFunction.IndexOf('[', methodStartIndex);

                    // We don't want to consume the indexer
                    if (indexerIndex >= 0 && indexerIndex < nextMethodIndex)
                    {
                        nextMethodIndex = indexerIndex;
                    }

                    functionArguments = new string[0];

                    if (nextMethodIndex > 0)
                    {
                        methodLength = nextMethodIndex - methodStartIndex;
                        remainder = expressionFunction.Substring(nextMethodIndex);
                    }

                    string netPropertyName = expressionFunction.Substring(methodStartIndex, methodLength).Trim();

                    ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                    // We have been asked for a property or a field
                    defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                    functionToInvoke = netPropertyName;
                }

                // either there are no functions left or what we have is another function or an indexer
                if (String.IsNullOrEmpty(remainder) || remainder[0] == '.' || remainder[0] == '[')
                {
                    // Construct a FunctionInfo will all the content that we just gathered
                    return new Function<T>(objectType, expressionFunction, expressionRootName, functionToInvoke, functionArguments, defaultBindingFlags, remainder, usedUninitializedProperties);
                }
                else
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    return null;
                }
            }

            /// <summary>
            /// Coerce the arguments according to the parameter types
            /// Will only return null if the coercion didn't work due to an InvalidCastException
            /// </summary>
            private static object[] CoerceArguments(object[] args, ParameterInfo[] parameters)
            {
                object[] coercedArguments = new object[args.Length];

                try
                {
                    // Do our best to coerce types into the arguments for the function
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        if (args[n] == null)
                        {
                            // We can't coerce (object)null -- that's as general
                            // as it can get!
                            continue;
                        }

                        // Here we have special case conversions on a type basis
                        if (parameters[n].ParameterType == typeof(char[]))
                        {
                            coercedArguments[n] = args[n].ToString().ToCharArray();
                        }
                        else if (parameters[n].ParameterType.IsEnum && args[n] is string && ((string)args[n]).Contains("."))
                        {
                            Type enumType = parameters[n].ParameterType;
                            string typeLeafName = enumType.Name + ".";
                            string typeFullName = enumType.FullName + ".";

                            // Enum.parse expects commas between enum components
                            // We'll support the C# type | syntax too
                            // We'll also allow the user to specify the leaf or full type name on the enum
                            string argument = args[n].ToString().Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

                            // Parse the string representation of the argument into the destination enum                                
                            coercedArguments[n] = Enum.Parse(enumType, argument);
                        }
                        else
                        {
                            // change the type of the final unescaped string into the destination
                            coercedArguments[n] = Convert.ChangeType(args[n], parameters[n].ParameterType, CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // The coercion failed therefore we return null
                    return null;
                }

                return coercedArguments;
            }

            /// <summary>
            /// Make an attempt to create a string showing what we were trying to execute when we failed.
            /// This will show any intermediate evaluation which may help the user figure out what happened.
            /// </summary>
            private string GenerateStringOfMethodExecuted(string expression, object objectInstance, string name, object[] args)
            {
                string parameters = String.Empty;
                if (args != null)
                {
                    foreach (object arg in args)
                    {
                        if (arg == null)
                        {
                            parameters += "null";
                        }
                        else
                        {
                            string argString = arg.ToString();
                            if (arg is string && argString.Length == 0)
                            {
                                parameters += "''";
                            }
                            else
                            {
                                parameters += arg.ToString();
                            }
                        }

                        parameters += ", ";
                    }

                    if (parameters.Length > 2)
                    {
                        parameters = parameters.Substring(0, parameters.Length - 2);
                    }
                }

                if (objectInstance == null)
                {
                    string typeName = _objectType.FullName;

                    // We don't want to expose the real type name of our intrinsics
                    // so we'll replace it with "MSBuild"
                    if (_objectType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                    {
                        typeName = "MSBuild";
                    }

                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return "[" + typeName + "]::" + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return "[" + typeName + "]::" + name;
                    }
                }
                else
                {
                    string propertyValue = "\"" + objectInstance as string + "\"";

                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return propertyValue + "." + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return propertyValue + "." + name;
                    }
                }
            }

            /// <summary>
            /// For this initial implementation of inline functions, only very specific static methods on specific types are
            /// available
            /// </summary>
            private bool IsStaticMethodAvailable(Type objectType, string methodName)
            {
                if (objectType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                {
                    // These are our intrinsic functions, so we're OK with those
                    return true;
                }
                else
                {
                    string typeMethod = objectType.FullName + "::" + methodName;

                    if (AvailableStaticMethods.ContainsKey(objectType.FullName))
                    {
                        // Check our set for the type name
                        // This enables all statics on the given type
                        return true;
                    }
                    else if (AvailableStaticMethods.ContainsKey(typeMethod))
                    {
                        // Check for specific methods on types
                        return true;
                    }
                    else if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                    {
                        // If MSBUILDENABLEALLPROPERTYFUNCTION == 1, then anything goes
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Construct and instance of objectType based on the constructor or method arguments provided.
            /// Arguments must never be null.
            /// </summary>
            private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
            {
                ParameterInfo[] parameters = null;
                MethodBase[] members = null;
                MethodBase memberInfo = null;

                // First let's try for a method where all arguments are strings..
                Type[] types = new Type[_arguments.Length];
                for (int n = 0; n < _arguments.Length; n++)
                {
                    types[n] = typeof(string);
                }

                if (isConstructor)
                {
                    memberInfo = _objectType.GetConstructor(bindingFlags, null, types, null);
                }
                else
                {
                    memberInfo = _objectType.GetMethod(_name, bindingFlags, null, types, null);
                }

                // If we didn't get a match on all string arguments,
                // search for a method with the right number of arguments
                if (memberInfo == null)
                {
                    // Gather all methods that may match
                    if (isConstructor)
                    {
                        members = _objectType.GetConstructors(bindingFlags);
                    }
                    else
                    {
                        members = _objectType.GetMethods(bindingFlags);
                    }

                    // Try to find a method with the right name, number of arguments and
                    // compatible argument types
                    object[] coercedArguments = null;
                    foreach (MethodBase member in members)
                    {
                        parameters = member.GetParameters();

                        // Simple match on name and number of params, we will be case insensitive
                        if (parameters.Length == _arguments.Length)
                        {
                            if (isConstructor || String.Equals(member.Name, _name, StringComparison.OrdinalIgnoreCase))
                            {
                                // we have a match on the name and argument number
                                // now let's try to coerce the arguments we have
                                // into the arguments on the matching method
                                coercedArguments = CoerceArguments(args, parameters);

                                if (coercedArguments != null)
                                {
                                    // We have a complete match
                                    memberInfo = member;
                                    args = coercedArguments;
                                    break;
                                }
                            }
                        }
                    }
                }

                object functionResult = null;

                // We have a match and coerced arguments, let's construct..
                if (memberInfo != null && args != null)
                {
                    if (isConstructor)
                    {
                        functionResult = ((ConstructorInfo)memberInfo).Invoke(args);
                    }
                    else
                    {
                        functionResult = ((MethodInfo)memberInfo).Invoke(objectInstance /* null if static method */, args);
                    }
                }
                else if (!isConstructor)
                {
                    throw ex;
                }

                if (functionResult == null && isConstructor)
                {
                    throw new TargetInvocationException(new MissingMethodException());
                }

                return functionResult;
            }
        }
    }

    /// <summary>
    /// This class wraps information about properties which have been used before they are initialized 
    /// </summary>
    internal class UsedUninitializedProperties
    {
        /// <summary>
        /// This class wraps information about properties which have been used before they are initialized
        /// </summary>
        internal UsedUninitializedProperties()
        {
            Properties = new Dictionary<string, IElementLocation>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Hash set of properties which have been used before being initialized
        /// </summary>
        internal IDictionary<string, IElementLocation> Properties
        {
            get;
            set;
        }

        /// <summary>
        ///  Are we currently supposed to warn if we used an uninitialized property.
        /// </summary>
        internal bool Warn
        {
            get;
            set;
        }

        /// <summary>
        ///  What is the currently evaluating property element, this is so that we do not add a un initialized property if we are evaluating that property
        /// </summary>
        internal string CurrentlyEvaluatingPropertyElementName
        {
            get;
            set;
        }
    }
}
