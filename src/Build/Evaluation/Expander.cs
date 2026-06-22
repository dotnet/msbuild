// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;

#nullable disable

namespace Microsoft.Build.Evaluation;

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
/// <typeparam name="P">Type of the properties used.</typeparam>
/// <typeparam name="I">Type of the items used.</typeparam>
internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// A limit for truncating string expansions within an evaluated Condition. Properties, item metadata, or item groups will be truncated to N characters such as 'N...'.
    /// Enabled by ExpanderOptions.Truncate.
    /// </summary>
    private const int CharacterLimitPerExpansion = 1024;
    /// <summary>
    /// A limit for truncating string expansions for item groups within an evaluated Condition. N items will be evaluated such as 'A;B;C;...'.
    /// Enabled by ExpanderOptions.Truncate.
    /// </summary>
    private const int ItemLimitPerExpansion = 3;

    /// <summary>
    /// The CultureInfo from the invariant culture. Used to avoid allocations for
    /// performing IndexOf etc.
    /// </summary>
    private static readonly CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>
    /// Properties to draw on for expansion.
    /// </summary>
    private IPropertyProvider<P> _properties;

    /// <summary>
    /// Items to draw on for expansion.
    /// </summary>
    private IItemProvider<I> _items;

    /// <summary>
    /// Metadata to draw on for expansion.
    /// </summary>
    private IMetadataTable _metadata;

    /// <summary>
    /// Set of properties which are null during expansion.
    /// </summary>
    private PropertiesUseTracker _propertiesUseTracker;

    private readonly IFileSystem _fileSystem;

    private readonly LoggingContext _loggingContext;

    /// <summary>
    /// Non-null if the expander was constructed for evaluation.
    /// </summary>
    internal EvaluationContext EvaluationContext { get; }

    private Expander(IPropertyProvider<P> properties, LoggingContext loggingContext)
    {
        _properties = properties;
        _propertiesUseTracker = new PropertiesUseTracker(loggingContext);
        _loggingContext = loggingContext;
    }

    /// <summary>
    /// Creates an expander passing it some properties to use.
    /// Properties may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, loggingContext)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Creates an expander passing it some properties to use.
    /// Properties may be null.
    ///
    /// Used for tests and for ToolsetReader - that operates agnostic on the project
    ///   - so no logging context is passed, and no BuildCheck check will be executed.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IFileSystem fileSystem)
    : this(properties, fileSystem, null)
    { }

    /// <summary>
    /// Creates an expander passing it some properties to use and the evaluation context.
    /// Properties may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        : this(properties, loggingContext)
    {
        _fileSystem = evaluationContext.FileSystem;
        EvaluationContext = evaluationContext;
    }

    /// <summary>
    /// Creates an expander passing it some properties and items to use.
    /// Either or both may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, fileSystem, loggingContext)
    {
        _items = items;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Expander{P, I}"/> class.
    /// Creates an expander passing it some properties and items to use, and the evaluation context.
    /// Either or both may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, EvaluationContext evaluationContext, LoggingContext loggingContext)
        : this(properties, evaluationContext, loggingContext)
    {
        _items = items;
    }

    /// <summary>
    /// Creates an expander passing it some properties, items, and/or metadata to use.
    /// Any or all may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IMetadataTable metadata, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, items, fileSystem, loggingContext)
    {
        _metadata = metadata;
    }

    /// <summary>
    /// Creates an expander passing it some properties, items, and/or metadata to use.
    /// Any or all may be null.
    ///
    /// This is for the purpose of evaluations through API calls, that might not be able to pass the logging context
    ///  - BuildCheck checking won't be executed for those.
    /// (for one of the calls we can actually pass IDataConsumingContext - as we have logging service and project)
    ///
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IMetadataTable metadata, IFileSystem fileSystem)
        : this(properties, items, fileSystem, null)
    {
        _metadata = metadata;
    }

    private Expander(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        IMetadataTable metadata,
        IFileSystem fileSystem,
        EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        : this(properties, items, metadata, fileSystem, loggingContext)
    {
        EvaluationContext = evaluationContext;
    }

    /// <summary>
    /// Recreates the expander with passed in logging context
    /// </summary>
    /// <param name="loggingContext"></param>
    /// <returns></returns>
    internal Expander<P, I> WithLoggingContext(LoggingContext loggingContext)
    {
        return new Expander<P, I>(_properties, _items, _metadata, _fileSystem, EvaluationContext, loggingContext);
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
    /// If a property is expanded but evaluates to null then it is considered to be un-initialized.
    /// We want to keep track of these properties so that we can warn if the property gets set later on.
    /// </summary>
    internal PropertiesUseTracker PropertiesUseTracker
    {
        get { return _propertiesUseTracker; }
        set { _propertiesUseTracker = value; }
    }

    /// <summary>
    /// Tests to see if the expression may contain expandable expressions, i.e.
    /// contains $, % or @.
    /// </summary>
    internal static bool ExpressionMayContainExpandableExpressions(string expression)
    {
        return expression.AsSpan().IndexOfAny('$', '%', '@') >= 0;
    }

    /// <summary>
    /// Returns true if the expression contains an item vector pattern, else returns false.
    /// Used to flag use of item expressions where they are illegal.
    /// </summary>
    internal static bool ExpressionContainsItemVector(string expression)
    {
        ExpressionShredder.ReferencedItemExpressionsEnumerator transformsEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

        return transformsEnumerator.MoveNext();
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

        return (result == null) ? null : EscapingUtilities.UnescapeAll(result);
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

        Assumed.NotNull(elementLocation);

        string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation, _loggingContext);
        result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        result = ItemExpander.ExpandItemVectorsIntoString(this, result, _items, options, elementLocation);
        result = FileUtilities.MaybeAdjustFilePath(result);

        return result;
    }

    /// <summary>
    /// Used only for unit tests. Expands the property expression (including any metadata expressions) and returns
    /// the result typed (i.e. not converted into a string if the result is a function return).
    /// </summary>
    internal object ExpandPropertiesLeaveTypedAndEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return String.Empty;
        }

        Assumed.NotNull(elementLocation);

        string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
        return PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(metaExpanded, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options,
    /// then splits on semi-colons into a list of strings.
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    /// </summary>
    internal SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        Assumed.True((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

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
    /// <typeparam name="T">Type of items to return.</typeparam>
    internal IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        Assumed.NotNull(elementLocation);

        expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
        expression = PropertyExpander.ExpandPropertiesLeaveEscaped(expression, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        expression = FileUtilities.MaybeAdjustFilePath(expression);

        List<T> result = new List<T>();

        if (expression.Length == 0)
        {
            return result;
        }

        var splits = ExpressionShredder.SplitSemiColonSeparatedList(expression);
        foreach (string split in splits)
        {
            bool isTransformExpression;
            IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, split, _items, itemFactory, options, false /* do not include null items */, out isTransformExpression, elementLocation);

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
    /// <typeparam name="T">Type of the items that should be returned.</typeparam>
    internal IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            isTransformExpression = false;
            return Array.Empty<T>();
        }

        Assumed.NotNull(elementLocation);

        return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, expression, _items, itemFactory, options, includeNullItems, out isTransformExpression, elementLocation);
    }

    internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
            string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        return ItemExpander.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
    }

    internal IList<T> ExpandExpressionCaptureIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture expressionCapture, IItemProvider<I> items, IItemFactory<I, T> itemFactory,
        ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem
    {
        return ItemExpander.ExpandExpressionCaptureIntoItems(expressionCapture, this, items, itemFactory, options,
            includeNullEntries, out isTransformExpression, elementLocation);
    }

    internal bool ExpandExpressionCapture(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IElementLocation elementLocation,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<TransformEntry> entries)
    {
        return ItemExpander.ExpandExpressionCapture(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out entries);
    }

    private static string TruncateString(string metadataValue)
    {
#if NET
        metadataValue = string.Concat(metadataValue.AsSpan(0, CharacterLimitPerExpansion - 3), "...");
#else
        // PERF: We need the formatted, truncated string. Using something like a StringBuilder avoids
        // needing to use an unsafe block, but this is more efficient.
        string truncatedMetadataValue = metadataValue.Substring(0, CharacterLimitPerExpansion);
        unsafe
        {
            fixed (char* truncatedMetadataPointer = truncatedMetadataValue)
            {
                Span<char> destination = new Span<char>(truncatedMetadataPointer, truncatedMetadataValue.Length);
                "...".AsSpan().CopyTo(destination.Slice(CharacterLimitPerExpansion - 3));
                metadataValue = truncatedMetadataValue;
            }
        }
#endif
        return metadataValue;
    }

    /// <summary>
    /// Returns true if the supplied string contains a valid property name.
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
    /// Returns true if ExpanderOptions.Truncate is set and EscapeHatches.DoNotTruncateConditions is not set.
    /// </summary>
    private static bool IsTruncationEnabled(ExpanderOptions options)
    {
        return (options & ExpanderOptions.Truncate) != 0 && !Traits.Instance.EscapeHatches.DoNotTruncateConditions;
    }

    /// <summary>
    /// Scan for the closing bracket that matches the one we've already skipped;
    /// essentially, pushes and pops on a stack of parentheses to do this.
    /// Takes the expression and the index to start at.
    /// Returns the index of the matching parenthesis, or -1 if it was not found.
    /// Also returns flags to indicate if a propertyfunction or registry property is likely
    /// to be found in the expression.
    /// </summary>
    private static int ScanForClosingParenthesis(ReadOnlySpan<char> expression, int index, out bool potentialPropertyFunction, out bool potentialRegistryFunction)
    {
        int nestLevel = 1;
        int length = expression.Length;

        potentialPropertyFunction = false;
        potentialRegistryFunction = false;

        // Scan for our closing ')'
        while (index < length && nestLevel > 0)
        {
            char character = expression[index];
            switch (character)
            {
                case '\'':
                case '`':
                case '"':
                    {
                        index++;
                        index = ScanForClosingQuote(character, expression, index);

                        if (index < 0)
                        {
                            return -1;
                        }
                        break;
                    }
                case '(':
                    {
                        nestLevel++;
                        break;
                    }
                case ')':
                    {
                        nestLevel--;
                        break;
                    }
                case '.':
                case '[':
                case '$':
                    {
                        potentialPropertyFunction = true;
                        break;
                    }
                case ':':
                    {
                        potentialRegistryFunction = true;
                        break;
                    }
            }

            index++;
        }

        // We will have parsed past the ')', so step back one character
        index--;

        return (nestLevel == 0) ? index : -1;
    }

    /// <summary>
    /// Skip all characters until we find the matching quote character.
    /// </summary>
    private static int ScanForClosingQuote(char quoteChar, ReadOnlySpan<char> expression, int index)
    {
        // Scan for our closing quoteChar
        int foundIndex = expression.Slice(index).IndexOf(quoteChar);
        return foundIndex < 0 ? -1 : foundIndex + index;
    }

    /// <summary>
    /// Extract the argument from the StringBuilder, handling nulls appropriately.
    /// </summary>
    private static string ExtractArgument(SpanBasedStringBuilder argumentBuilder)
    {
        // we reached the end of an argument, add the builder's final result
        // to our arguments.
        argumentBuilder.Trim();

        // We support passing of null through the argument constant value null
        if (argumentBuilder.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else
        {
            if (argumentBuilder.Length > 0)
            {
                if (argumentBuilder[0] == '\'' && argumentBuilder[argumentBuilder.Length - 1] == '\'')
                {
                    argumentBuilder.Trim('\'');
                }
                else if (argumentBuilder[0] == '`' && argumentBuilder[argumentBuilder.Length - 1] == '`')
                {
                    argumentBuilder.Trim('`');
                }
                else if (argumentBuilder[0] == '"' && argumentBuilder[argumentBuilder.Length - 1] == '"')
                {
                    argumentBuilder.Trim('"');
                }

                return argumentBuilder.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Extract the first level of arguments from the content.
    /// Splits the content passed in at commas.
    /// Returns an array of unexpanded arguments.
    /// If there are no arguments, returns an empty array.
    /// </summary>
    private static string[] ExtractFunctionArguments(IElementLocation elementLocation, string expressionFunction, ReadOnlyMemory<char> argumentsMemory)
    {
        int argumentsContentLength = argumentsMemory.Length;
        ReadOnlySpan<char> argumentsSpan = argumentsMemory.Span;

        using SpanBasedStringBuilder argumentBuilder = Strings.GetSpanBasedStringBuilder();
        int? argumentStartIndex = null;

        // We iterate over the string in the for loop below. When we find an argument, instead of adding it to the argument
        // builder one-character-at-a-time, we remember the start index and then call this function when we find the end of
        // the argument. This appends the entire {start, end} span to the builder in one call.
        void FlushCurrentArgumentToArgumentBuilder(int argumentEndIndex)
        {
            if (argumentStartIndex.HasValue)
            {
                argumentBuilder.Append(argumentsMemory.Slice(argumentStartIndex.Value, argumentEndIndex - argumentStartIndex.Value));
                argumentStartIndex = null;
            }
        }

        // Iterate over the contents of the arguments extracting the
        // the individual arguments as we go
        List<string> arguments = null;
        for (int n = 0; n < argumentsContentLength; n++)
        {
            // We found a property expression.. skip over all of it.
            if ((n < argumentsContentLength - 1) && (argumentsSpan[n] == '$' && argumentsSpan[n + 1] == '('))
            {
                int nestedPropertyStart = n;
                n += 2; // skip over the opening '$('

                // Scan for the matching closing bracket, skipping any nested ones
                n = ScanForClosingParenthesis(argumentsSpan, n, out _, out _);

                if (n == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                }

                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: nestedPropertyStart);
                argumentBuilder.Append(argumentsMemory.Slice(nestedPropertyStart, (n - nestedPropertyStart) + 1));
            }
            else if (argumentsSpan[n] == '`' || argumentsSpan[n] == '"' || argumentsSpan[n] == '\'')
            {
                int quoteStart = n;
                n++; // skip over the opening quote

                n = ScanForClosingQuote(argumentsSpan[quoteStart], argumentsSpan, n);

                if (n == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedQuote"));
                }

                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: quoteStart);
                argumentBuilder.Append(argumentsMemory.Slice(quoteStart, (n - quoteStart) + 1));
            }
            else if (argumentsSpan[n] == ',')
            {
                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: n);

                // We have reached the end of the current argument, go ahead and add it
                // to our list
                if (arguments is null)
                {
                    // get an upper limit for the size of the arguments list.
                    int argumentCount = 2;
                    for (int i = n + 1; i < argumentsContentLength; ++i)
                    {
                        if (argumentsSpan[i] == ',')
                        {
                            argumentCount++;
                        }
                    }

                    arguments = new List<string>(argumentCount);
                }

                arguments.Add(ExtractArgument(argumentBuilder));

                // Clear out the argument builder ready for the next argument
                argumentBuilder.Clear();
            }
            else
            {
                argumentStartIndex ??= n;
            }
        }

        // We reached the end of the string but we may have seen the start but not the end of the last (or only) argument so flush it now.
        FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: argumentsContentLength);

        // This will either be the one and only argument, or the last one
        // so add it to our list
        string finalArgument = ExtractArgument(argumentBuilder);
        if (arguments is null)
        {
            return [finalArgument];
        }
        else
        {
            arguments.Add(finalArgument);

            return arguments.ToArray();
        }
    }
}
