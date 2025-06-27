// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if NET
using System.IO;
#else
using Microsoft.IO;
#endif
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using Microsoft.Win32;
using AvailableStaticMethods = Microsoft.Build.Internal.AvailableStaticMethods;
using ItemSpecModifiers = Microsoft.Build.Shared.FileUtilities.ItemSpecModifiers;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;

#nullable disable

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
        /// When an error occurs expanding a property, just leave it unexpanded.
        /// </summary>
        /// <remarks>
        /// This should only be used in cases where property evaluation isn't critical, such as when attempting to log a
        /// message with a best effort expansion of a string, or when discovering partial information during lazy evaluation.
        /// </remarks>
        LeavePropertiesUnexpandedOnError = 0x20,

        /// <summary>
        /// When an expansion occurs, truncate it to Expander.DefaultTruncationCharacterLimit or Expander.DefaultTruncationItemLimit.
        /// </summary>
        Truncate = 0x40,

        /// <summary>
        /// Issues build message if item references unqualified or qualified metadata odf self - as this can lead to unintended expansion and
        ///  cross-combination of other items.
        /// More info: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-batching#item-batching-on-self-referencing-metadata
        /// </summary>
        LogOnItemMetadataSelfReference = 0x80,

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
    /// <typeparam name="P">Type of the properties used.</typeparam>
    /// <typeparam name="I">Type of the items used.</typeparam>
    internal partial class Expander<P, I>
        where P : class, IProperty
        where I : class, IItem
    {
        /// <summary>
        /// A helper struct wrapping a <see cref="SpanBasedStringBuilder"/> and providing file path conversion
        /// as used in e.g. property expansion.
        /// </summary>
        /// <remarks>
        /// If exactly one value is added and no concatenation takes places, this value is returned without
        /// conversion. In other cases values are stringified and attempted to be interpreted as file paths
        /// before concatenation.
        /// </remarks>
        private struct SpanBasedConcatenator : IDisposable
        {
            /// <summary>
            /// The backing <see cref="SpanBasedStringBuilder"/>, null until the second value is added.
            /// </summary>
            private SpanBasedStringBuilder _builder;

            /// <summary>
            /// The first value added to the concatenator. Tracked in its own field so it can be returned
            /// without conversion if no concatenation takes place.
            /// </summary>
            private object _firstObject;

            /// <summary>
            /// The first value added to the concatenator if it is a span. Tracked in its own field so the
            /// <see cref="SpanBasedStringBuilder"/> functionality doesn't have to be invoked if no concatenation
            /// takes place.
            /// </summary>
            private ReadOnlyMemory<char> _firstSpan;

            /// <summary>
            /// True if this instance is already disposed.
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// Adds an object to be concatenated.
            /// </summary>
            public void Add(object obj)
            {
                CheckDisposed();
                FlushFirstValueIfNeeded();
                if (_builder != null)
                {
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(obj.ToString()));
                }
                else
                {
                    _firstObject = obj;
                }
            }

            /// <summary>
            /// Adds a span to be concatenated.
            /// </summary>
            public void Add(ReadOnlyMemory<char> span)
            {
                CheckDisposed();
                FlushFirstValueIfNeeded();
                if (_builder != null)
                {
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(span));
                }
                else
                {
                    _firstSpan = span;
                }
            }

            /// <summary>
            /// Returns the result of the concatenation.
            /// </summary>
            /// <returns>
            /// If only one value has been added and it is not a string, it is returned unchanged.
            /// In all other cases (no value, one string value, multiple values) the result is a
            /// concatenation of the string representation of the values, each additionally subjected
            /// to file path adjustment.
            /// </returns>
            public readonly object GetResult()
            {
                CheckDisposed();
                if (_firstObject != null)
                {
                    return (_firstObject is string stringValue) ? FileUtilities.MaybeAdjustFilePath(stringValue) : _firstObject;
                }
                return _firstSpan.IsEmpty
                    ? _builder?.ToString() ?? string.Empty
                    : FileUtilities.MaybeAdjustFilePath(_firstSpan).ToString();
            }

            /// <summary>
            /// Disposes of the struct by delegating the call to the underlying <see cref="SpanBasedStringBuilder"/>.
            /// </summary>
            public void Dispose()
            {
                CheckDisposed();
                _builder?.Dispose();
                _disposed = true;
            }

            /// <summary>
            /// Throws <see cref="ObjectDisposedException"/> if this concatenator is already disposed.
            /// </summary>
            private readonly void CheckDisposed() =>
                ErrorUtilities.VerifyThrowObjectDisposed(!_disposed, nameof(SpanBasedConcatenator));

            /// <summary>
            /// Lazily initializes <see cref="_builder"/> and populates it with the first value
            /// when the second value is being added.
            /// </summary>
            private void FlushFirstValueIfNeeded()
            {
                if (_firstObject != null)
                {
                    _builder = Strings.GetSpanBasedStringBuilder();
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstObject.ToString()));
                    _firstObject = null;
                }
                else if (!_firstSpan.IsEmpty)
                {
                    _builder = Strings.GetSpanBasedStringBuilder();
#if FEATURE_SPAN
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan));
#else
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan.ToString()));
#endif
                    _firstSpan = new ReadOnlyMemory<char>();
                }
            }
        }

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
        private static readonly char[] s_singleQuoteChar = { '\'' };
        private static readonly char[] s_backtickChar = { '`' };
        private static readonly char[] s_doubleQuoteChar = { '"' };

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
            List<ExpressionShredder.ItemExpressionCapture> transforms = ExpressionShredder.GetReferencedItemExpressions(expression);

            return transforms != null;
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

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation, _loggingContext);
            result = PropertyExpander<P>.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
            result = ItemExpander.ExpandItemVectorsIntoString<I>(this, result, _items, options, elementLocation);
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

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
            return PropertyExpander<P>.ExpandPropertiesLeaveTypedAndEscaped(metaExpanded, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options,
        /// then splits on semi-colons into a list of strings.
        /// Use this form when the result is going to be processed further, for example by matching against the file system,
        /// so literals must be distinguished, and you promise to unescape after that.
        /// </summary>
        internal SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
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
        /// <typeparam name="T">Type of items to return.</typeparam>
        internal IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
            where T : class, IItem
        {
            if (expression.Length == 0)
            {
                return Array.Empty<T>();
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
            expression = PropertyExpander<P>.ExpandPropertiesLeaveEscaped(expression, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
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
        /// <typeparam name="T">Type of the items that should be returned.</typeparam>
        internal IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            if (expression.Length == 0)
            {
                isTransformExpression = false;
                return Array.Empty<T>();
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

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

        internal bool ExpandExpressionCapture(
            ExpressionShredder.ItemExpressionCapture expressionCapture,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<KeyValuePair<string, I>> itemsFromCapture)
        {
            return ItemExpander.ExpandExpressionCapture(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out itemsFromCapture);
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
        /// Add the argument in the StringBuilder to the arguments list, handling nulls
        /// appropriately.
        /// </summary>
        private static void AddArgument(List<string> arguments, SpanBasedStringBuilder argumentBuilder)
        {
            // we reached the end of an argument, add the builder's final result
            // to our arguments.
            argumentBuilder.Trim();
            string argValue = argumentBuilder.ToString();

            // We support passing of null through the argument constant value null
            if (String.Equals("null", argValue, StringComparison.OrdinalIgnoreCase))
            {
                arguments.Add(null);
            }
            else
            {
                if (argValue.Length > 0)
                {
                    if (argValue[0] == '\'' && argValue[argValue.Length - 1] == '\'')
                    {
                        arguments.Add(argValue.Trim(s_singleQuoteChar));
                    }
                    else if (argValue[0] == '`' && argValue[argValue.Length - 1] == '`')
                    {
                        arguments.Add(argValue.Trim(s_backtickChar));
                    }
                    else if (argValue[0] == '"' && argValue[argValue.Length - 1] == '"')
                    {
                        arguments.Add(argValue.Trim(s_doubleQuoteChar));
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
        private static string[] ExtractFunctionArguments(IElementLocation elementLocation, string expressionFunction, ReadOnlyMemory<char> argumentsMemory)
        {
            int argumentsContentLength = argumentsMemory.Length;
            ReadOnlySpan<char> argumentsSpan = argumentsMemory.Span;

            List<string> arguments = new List<string>();

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
                    AddArgument(arguments, argumentBuilder);

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
            AddArgument(arguments, argumentBuilder);

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
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);
                        result = RegularExpressions.ItemMetadataRegex.Replace(expression, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));
                    }
                    else
                    {
                        List<ExpressionShredder.ItemExpressionCapture> itemVectorExpressions = ExpressionShredder.GetReferencedItemExpressions(expression);

                        // The most common case is where the transform is the whole expression
                        // Also if there were no valid item vector expressions found, then go ahead and do the replacement on
                        // the whole expression (which is what Orcas did).
                        if (itemVectorExpressions?.Count == 1 && itemVectorExpressions[0].Value == expression && itemVectorExpressions[0].Separator == null)
                        {
                            return expression;
                        }

                        // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                        using SpanBasedStringBuilder finalResultBuilder = Strings.GetSpanBasedStringBuilder();

                        int start = 0;
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);

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
                                string replacementResult = RegularExpressions.NonTransformItemMetadataRegex.Replace(subExpressionToReplaceIn, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));

                                // Append the metadata replacement
                                finalResultBuilder.Append(replacementResult);

                                // Expand any metadata that appears in the item vector expression's separator
                                if (itemVectorExpressions[n].Separator != null)
                                {
                                    vectorExpression = RegularExpressions.NonTransformItemMetadataRegex.Replace(itemVectorExpressions[n].Value, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata), -1, itemVectorExpressions[n].SeparatorStart);
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
                            string replacementResult = RegularExpressions.NonTransformItemMetadataRegex.Replace(subExpressionToReplaceIn, new MatchEvaluator(matchEvaluator.ExpandSingleMetadata));

                            finalResultBuilder.Append(replacementResult);
                        }

                        result = finalResultBuilder.ToString();
                    }

                    // Don't create more strings
                    if (String.Equals(result, expression, StringComparison.Ordinal))
                    {
                        result = expression;
                    }

                    return result;
                }
                catch (InvalidOperationException ex)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression, ex.Message);
                }

                return null;
            }

            /// <summary>
            /// A functor that returns the value of the metadata in the match
            /// that is contained in the metadata dictionary it was created with.
            /// </summary>
            private class MetadataMatchEvaluator
            {
                /// <summary>
                /// Source of the metadata.
                /// </summary>
                private IMetadataTable _metadata;

                /// <summary>
                /// Whether to expand built-in metadata, custom metadata, or both kinds.
                /// </summary>
                private ExpanderOptions _options;

                private IElementLocation _elementLocation;

                private LoggingContext _loggingContext;

                /// <summary>
                /// Constructor taking a source of metadata.
                /// </summary>
                internal MetadataMatchEvaluator(
                    IMetadataTable metadata,
                    ExpanderOptions options,
                    IElementLocation elementLocation,
                    LoggingContext loggingContext)
                {
                    _metadata = metadata;
                    _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
                    _elementLocation = elementLocation;
                    _loggingContext = loggingContext;

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
                       (!isBuiltInMetadata && ((_options & ExpanderOptions.ExpandCustomMetadata) != 0)))
                    {
                        metadataValue = _metadata.GetEscapedValue(itemType, metadataName);

                        if ((_options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 &&
                            _loggingContext != null &&
                            !string.IsNullOrEmpty(metadataName) &&
                            _metadata is IItemTypeDefinition itemMetadata &&
                            (string.IsNullOrEmpty(itemType) || string.Equals(itemType, itemMetadata.ItemType, StringComparison.Ordinal)))
                        {
                            _loggingContext.LogComment(MessageImportance.Low, new BuildEventFileInfo(_elementLocation),
                                "ItemReferencingSelfInTarget", itemMetadata.ItemType, metadataName);
                        }

                        if (IsTruncationEnabled(_options) && metadataValue.Length > CharacterLimitPerExpansion)
                        {
                            metadataValue =
#if NET
                                $"{metadataValue.AsSpan(0, CharacterLimitPerExpansion - 3)}...";
#else
                                $"{metadataValue.Substring(0, CharacterLimitPerExpansion - 3)}...";
#endif
                        }
                    }

                    return metadataValue;
                }
            }
        }

        /// <summary>
        /// Expands property expressions, like $(Configuration) and $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation).
        /// </summary>
        /// <remarks>
        /// This is a private nested class, exposed only through the Expander class.
        /// That allows it to hide its private methods even from Expander.
        /// </remarks>
        /// <typeparam name="T">Type of the properties used to expand the expression.</typeparam>
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
            internal static string ExpandPropertiesLeaveEscaped(
                string expression,
                IPropertyProvider<T> properties,
                ExpanderOptions options,
                IElementLocation elementLocation,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem)
            {
                return
                    ConvertToString(
                        ExpandPropertiesLeaveTypedAndEscaped(
                            expression,
                            properties,
                            options,
                            elementLocation,
                            propertiesUseTracker,
                            fileSystem));
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
            internal static object ExpandPropertiesLeaveTypedAndEscaped(
                string expression,
                IPropertyProvider<T> properties,
                ExpanderOptions options,
                IElementLocation elementLocation,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem)
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
                using Expander<P, I>.SpanBasedConcatenator results = new Expander<P, I>.SpanBasedConcatenator();

                // The sourceIndex is the zero-based index into the expression,
                // where we've essentially read up to and copied into the target string.
                int sourceIndex = 0;

                // Search for "$(" in the expression.  Loop until we don't find it
                // any more.
                while (propertyStartIndex != -1)
                {
                    // Append the result with the portion of the expression up to
                    // (but not including) the "$(", and advance the sourceIndex pointer.
                    if (propertyStartIndex - sourceIndex > 0)
                    {
                        results.Add(expression.AsMemory(sourceIndex, propertyStartIndex - sourceIndex));
                    }

                    // Following the "$(" we need to locate the matching ')'
                    // Scan for the matching closing bracket, skipping any nested ones
                    // This is a very complete, fast validation of parenthesis matching including for nested
                    // function calls.
                    propertyEndIndex = ScanForClosingParenthesis(expression.AsSpan(), propertyStartIndex + 2, out bool tryExtractPropertyFunction, out bool tryExtractRegistryFunction);

                    if (propertyEndIndex == -1)
                    {
                        // If we didn't find the closing parenthesis, that means this
                        // isn't really a well-formed property tag.  Just literally
                        // copy the remainder of the expression (starting with the "$("
                        // that we found) into the result, and quit.
                        results.Add(expression.AsMemory(propertyStartIndex, expression.Length - propertyStartIndex));
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
                        object propertyValue;

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
                            propertyValue = ExpandRegistryValue(propertyBody, elementLocation); // This func returns an empty string if not on Windows
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
                            propertyValue = ExpandPropertyBody(
                                propertyBody,
                                null,
                                properties,
                                options,
                                elementLocation,
                                propertiesUseTracker,
                                fileSystem);
                        }
                        else // This is a regular property
                        {
                            propertyValue = LookupProperty(properties, expression, propertyStartIndex + 2, propertyEndIndex - 1, elementLocation, propertiesUseTracker);
                        }

                        if (propertyValue != null)
                        {
                            if (IsTruncationEnabled(options))
                            {
                                var value = propertyValue.ToString();
                                if (value.Length > CharacterLimitPerExpansion)
                                {
                                    propertyValue =
#if NET
                                        $"{value.AsSpan(0, CharacterLimitPerExpansion - 3)}...";
#else
                                        $"{value.Substring(0, CharacterLimitPerExpansion - 3)}...";
#endif
                                }
                            }

                            // Record our result, and advance
                            // our sourceIndex pointer to the character just after the closing
                            // parenthesis.
                            results.Add(propertyValue);
                        }
                        sourceIndex = propertyEndIndex + 1;
                    }

                    propertyStartIndex = s_invariantCompareInfo.IndexOf(expression, "$(", sourceIndex, CompareOptions.Ordinal);
                }

                // If we couldn't find any more property tags in the expression just copy the remainder into the result.
                if (expression.Length - sourceIndex > 0)
                {
                    results.Add(expression.AsMemory(sourceIndex, expression.Length - sourceIndex));
                }

                return results.GetResult();
            }

            /// <summary>
            /// Expand the body of the property, including any functions that it may contain.
            /// </summary>
            internal static object ExpandPropertyBody(
                string propertyBody,
                object propertyValue,
                IPropertyProvider<T> properties,
                ExpanderOptions options,
                IElementLocation elementLocation,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem)
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
                    if (propertyBody.Contains('.') || propertyBody[0] == '[')
                    {
                        if (BuildParameters.DebugExpansion)
                        {
                            Console.WriteLine("Expanding: {0}", propertyBody);
                        }

                        // This is a function
                        function = Function<T>.ExtractPropertyFunction(
                            propertyBody,
                            elementLocation,
                            propertyValue,
                            propertiesUseTracker,
                            fileSystem,
                            propertiesUseTracker.LoggingContext);

                        // We may not have been able to parse out a function
                        if (function != null)
                        {
                            // We will have either extracted the actual property name
                            // or realized that there is none (static function), and have recorded a null
                            propertyName = function.Receiver;
                        }
                        else
                        {
                            // In the event that we have been handed an unrecognized property body, throw
                            // an invalid function property exception.
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                            return null;
                        }
                    }
                    else if (propertyValue == null && propertyBody.Contains('[')) // a single property indexer
                    {
                        int indexerStart = propertyBody.IndexOf('[');
                        int indexerEnd = propertyBody.IndexOf(']');

                        if (indexerStart < 0 || indexerEnd < 0)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                        }
                        else
                        {
                            propertyValue = LookupProperty(properties, propertyBody, 0, indexerStart - 1, elementLocation, propertiesUseTracker);
                            propertyBody = propertyBody.Substring(indexerStart);

                            // recurse so that the function representing the indexer can be executed on the property value
                            return ExpandPropertyBody(
                                propertyBody,
                                propertyValue,
                                properties,
                                options,
                                elementLocation,
                                propertiesUseTracker,
                                fileSystem);
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
                    propertyValue = LookupProperty(properties, propertyName, elementLocation, propertiesUseTracker);
                }

                if (function != null)
                {
                    try
                    {
                        // Because of the rich expansion capabilities of MSBuild, we need to keep things
                        // as strings, since property expansion & string embedding can happen anywhere
                        // propertyValue can be null here, when we're invoking a static function
                        propertyValue = function.Execute(propertyValue, properties, options, elementLocation);
                    }
                    catch (Exception) when (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                    {
                        propertyValue = propertyBody;
                    }
                }

                return propertyValue;
            }

            /// <summary>
            /// Convert the object into an MSBuild friendly string
            /// Arrays are supported.
            /// Will not return NULL.
            /// </summary>
            internal static string ConvertToString(object valueToConvert)
            {
                if (valueToConvert == null)
                {
                    return String.Empty;
                }
                // If the value is a string, then there is nothing to do
                if (valueToConvert is string stringValue)
                {
                    return stringValue;
                }

                string convertedString;
                if (valueToConvert is IDictionary dictionary)
                {
                    // If the return type is an IDictionary, then we convert this to
                    // a semi-colon delimited set of A=B pairs.
                    // Key and Value are converted to string and escaped
                    if (dictionary.Count > 0)
                    {
                        using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                        foreach (DictionaryEntry entry in dictionary)
                        {
                            if (builder.Length > 0)
                            {
                                builder.Append(";");
                            }

                            // convert and escape each key and value in the dictionary entry
                            builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                            builder.Append("=");
                            builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                        }

                        convertedString = builder.ToString();
                    }
                    else
                    {
                        convertedString = string.Empty;
                    }
                }
                else if (valueToConvert is IEnumerable enumerable)
                {
                    // If the return is enumerable, then we'll convert to semi-colon delimited elements
                    // each of which must be converted, so we'll recurse for each element
                    using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                    foreach (object element in enumerable)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(";");
                        }

                        // we need to convert and escape each element of the array
                        builder.Append(EscapingUtilities.Escape(ConvertToString(element)));
                    }

                    convertedString = builder.ToString();
                }
                else
                {
                    // The fall back is always to just convert to a string directly.
                    // Issue: https://github.com/dotnet/msbuild/issues/9757
                    if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12))
                    {
                        convertedString = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        convertedString = valueToConvert.ToString();
                    }
                }

                return convertedString;
            }

            /// <summary>
            /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
            /// </summary>
            private static object LookupProperty(IPropertyProvider<T> properties, string propertyName, IElementLocation elementLocation, PropertiesUseTracker propertiesUseTracker)
            {
                return LookupProperty(properties, propertyName, 0, propertyName.Length - 1, elementLocation, propertiesUseTracker);
            }

            /// <summary>
            /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
            /// </summary>
            private static object LookupProperty(IPropertyProvider<T> properties, string propertyName, int startIndex, int endIndex, IElementLocation elementLocation, PropertiesUseTracker propertiesUseTracker)
            {
                T property = properties.GetProperty(propertyName, startIndex, endIndex);

                object propertyValue;

                bool isArtificial = property == null && ((endIndex - startIndex) >= 7) &&
                                   MSBuildNameIgnoreCaseComparer.Default.Equals("MSBuild", propertyName, startIndex, 7);

                propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, elementLocation, property == null, isArtificial);

                if (isArtificial)
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
                    propertyValue = String.Empty;
                }
                else
                {
                    if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
                    {
                        environmentDerivedProperty.loggingContext = propertiesUseTracker.LoggingContext;
                    }

                    propertyValue = property.GetEvaluatedValueEscaped(elementLocation);
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
                    value = FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);
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
#if RUNTIME_TYPE_NETCORE
                // .NET Core MSBuild used to always return empty, so match that behavior
                // on non-Windows (no registry).
                if (!NativeMethodsShared.IsWindows)
                {
                    return string.Empty;
                }
#endif

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
                        // Unless we are running under Windows, don't bother with anything but the user keys
                        if (!NativeMethodsShared.IsWindows && !registryKeyName.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                        {
                            // Fake common requests to HKLM that we can resolve

                            // This is the base path of the framework
                            if (registryKeyName.StartsWith(
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework",
                                StringComparison.OrdinalIgnoreCase) &&
                                valueName.Equals("InstallRoot", StringComparison.OrdinalIgnoreCase))
                            {
                                return NativeMethodsShared.FrameworkBasePath + Path.DirectorySeparatorChar;
                            }

                            return string.Empty;
                        }

                        object valueFromRegistry = Registry.GetValue(registryKeyName, valueName, null /* default if key or value name is not found */);

                        if (valueFromRegistry != null)
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
                    catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", ex.Message);
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
        ///     "my list: @(files->'')                              expands to string      "my list: ;".
        /// </summary>
        /// <remarks>
        /// This is a private nested class, exposed only through the Expander class.
        /// That allows it to hide its private methods even from Expander.
        /// </remarks>
        private static class ItemExpander
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

            private enum ItemTransformFunctions
            {
                ItemSpecModifierFunction,
                Count,
                Exists,
                Combine,
                GetPathsOfAllDirectoriesAbove,
                DirectoryName,
                Metadata,
                DistinctWithCase,
                Distinct,
                Reverse,
                ExpandQuotedExpressionFunction,
                ExecuteStringFunction,
                ClearMetadata,
                HasMetadata,
                WithMetadataValue,
                WithoutMetadataValue,
                AnyHaveMetadataValue,
            }

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

                    if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
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

                    foreach (KeyValuePair<string, S> itemTuple in transformedItems)
                    {
                        if (!string.IsNullOrEmpty(itemTuple.Key) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                        {
                            brokeEarly = true;
                            return transformedItems; // break out early
                        }
                    }

                    // If we have another transform, swap the source and transform lists.
                    if (i < captures.Count - 1)
                    {
                        (transformedItems, sourceItems) = (sourceItems, transformedItems);
                        transformedItems.Clear();
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

                List<ExpressionShredder.ItemExpressionCapture> matches;
                if (!expression.Contains('@'))
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
                ErrorUtilities.VerifyThrow(evaluatedItems != null, "Cannot expand items without providing items");
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

                ErrorUtilities.VerifyThrow(items != null, "Cannot expand items without providing items");

                List<ExpressionShredder.ItemExpressionCapture> matches = ExpressionShredder.GetReferencedItemExpressions(expression);

                if (matches == null)
                {
                    return expression;
                }

                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
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

                    bool brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(expander, matches[i], items, elementLocation, builder, options);

                    if (brokeEarlyNonEmpty)
                    {
                        return null;
                    }

                    lastStringIndex = matches[i].Index + matches[i].Length;
                }

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

            /// <summary>
            /// The set of functions that called during an item transformation, e.g. @(CLCompile->ContainsMetadata('MetaName', 'metaValue')).
            /// </summary>
            /// <typeparam name="S">class, IItem.</typeparam>
            internal static class IntrinsicItemFunctions<S>
                where S : class, IItem
            {
                /// <summary>
                /// The number of characters added by a quoted expression.
                /// 3 characters for
                ///  </summary>
                private const int QuotedExpressionSurroundCharCount = 3;

                /// <summary>
                /// A precomputed lookup of item spec modifiers wrapped in regex strings.
                /// This allows us to completely skip of Regex parsing when the inner string matches a known modifier.
                /// IsDerivableItemSpecModifier doesn't currently support Span lookups, so we have to manually map these.
                /// </summary>
                private static readonly FrozenDictionary<string, string> s_itemSpecModifiers = new Dictionary<string, string>()
                {
                    [$"%({ItemSpecModifiers.FullPath})"] = ItemSpecModifiers.FullPath,
                    [$"%({ItemSpecModifiers.RootDir})"] = ItemSpecModifiers.RootDir,
                    [$"%({ItemSpecModifiers.Filename})"] = ItemSpecModifiers.Filename,
                    [$"%({ItemSpecModifiers.Extension})"] = ItemSpecModifiers.Extension,
                    [$"%({ItemSpecModifiers.RelativeDir})"] = ItemSpecModifiers.RelativeDir,
                    [$"%({ItemSpecModifiers.Directory})"] = ItemSpecModifiers.Directory,
                    [$"%({ItemSpecModifiers.RecursiveDir})"] = ItemSpecModifiers.RecursiveDir,
                    [$"%({ItemSpecModifiers.Identity})"] = ItemSpecModifiers.Identity,
                    [$"%({ItemSpecModifiers.ModifiedTime})"] = ItemSpecModifiers.ModifiedTime,
                    [$"%({ItemSpecModifiers.CreatedTime})"] = ItemSpecModifiers.CreatedTime,
                    [$"%({ItemSpecModifiers.AccessedTime})"] = ItemSpecModifiers.AccessedTime,
                    [$"%({ItemSpecModifiers.DefiningProjectFullPath})"] = ItemSpecModifiers.DefiningProjectFullPath,
                    [$"%({ItemSpecModifiers.DefiningProjectDirectory})"] = ItemSpecModifiers.DefiningProjectDirectory,
                    [$"%({ItemSpecModifiers.DefiningProjectName})"] = ItemSpecModifiers.DefiningProjectName,
                    [$"%({ItemSpecModifiers.DefiningProjectExtension})"] = ItemSpecModifiers.DefiningProjectExtension,
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

                /// <summary>
                /// A thread-static string builder for use in ExpandQuotedExpressionFunction.
                /// In theory we should be able to use shared instance, but in a profile it appears something higher in
                /// the call-stack is already borrowing the instance, so it ends up always allocating.
                /// This should not be used outside of ExpandQuotedExpressionFunction unless validated to not conflict.
                /// </summary>
                [ThreadStatic]
                private static SpanBasedStringBuilder s_includeBuilder;

                /// <summary>
                /// A reference to the last extracted expression function to save on Regex-related allocations.
                /// In many cases, the expression is exactly the same as the previous.
                /// </summary>
                private static string s_lastParsedQuotedExpression;

                /// <summary>
                /// Create an enumerator from a base IEnumerable of items into an enumerable
                /// of transformation result which includes the new itemspec and the base item.
                /// </summary>
                internal static List<KeyValuePair<string, S>> GetItemPairs(ICollection<S> itemsOfType)
                {
                    List<KeyValuePair<string, S>> itemsFromCapture = new(itemsOfType.Count);

                    // iterate over the items, and add items in the tuple format
                    foreach (S item in itemsOfType)
                    {
                        if (Traits.Instance.UseLazyWildCardEvaluation)
                        {
                            foreach (var resultantItem in
                                EngineFileUtilities.GetFileListEscaped(
                                    item.ProjectDirectory,
                                    item.EvaluatedIncludeEscaped,
                                    forceEvaluate: true))
                            {
                                itemsFromCapture.Add(new KeyValuePair<string, S>(resultantItem, item));
                            }
                        }
                        else
                        {
                            itemsFromCapture.Add(new KeyValuePair<string, S>(item.EvaluatedIncludeEscaped, item));
                        }
                    }

                    return itemsFromCapture;
                }

                /// <summary>
                /// Intrinsic function that adds the number of items in the list.
                /// </summary>
                internal static void Count(List<KeyValuePair<string, S>> itemsOfType, List<KeyValuePair<string, S>> transformedItems)
                {
                    transformedItems.Add(new KeyValuePair<string, S>(Convert.ToString(itemsOfType.Count, CultureInfo.InvariantCulture), null /* no base item */));
                }

                /// <summary>
                /// Intrinsic function that adds the specified built-in modifer value of the items in itemsOfType
                /// Tuple is {current item include, item under transformation}.
                /// </summary>
                internal static void ItemSpecModifierFunction(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        // If the item include has become empty,
                        // this is the end of the pipeline for this item
                        if (String.IsNullOrEmpty(item.Key))
                        {
                            continue;
                        }

                        string result = null;

                        try
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case, we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            string directoryToUse = item.Value.ProjectDirectory ?? Directory.GetCurrentDirectory();
                            string definingProjectEscaped = item.Value.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);

                            result = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(directoryToUse, item.Key, definingProjectEscaped, functionName);
                        }
                        // InvalidOperationException is how GetItemSpecModifier communicates invalid conditions upwards, so
                        // we do not want to rethrow in that case.
                        catch (Exception e) when (!ExceptionHandling.NotExpectedException(e) || e is InvalidOperationException)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                        }

                        if (!String.IsNullOrEmpty(result))
                        {
                            // GetItemSpecModifier will have returned us an escaped string
                            // there is nothing more to do than yield it into the pipeline
                            transformedItems.Add(new KeyValuePair<string, S>(result, item.Value));
                        }
                        else if (includeNullEntries)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds the subset of items that actually exist on disk.
                /// </summary>
                internal static void Exists(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (String.IsNullOrEmpty(item.Key))
                        {
                            continue;
                        }

                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

                        string rootedPath = null;
                        try
                        {
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
                                string baseDirectoryToUse = item.Value.ProjectDirectory ?? String.Empty;
                                rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                            }
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                        }

                        if (File.Exists(rootedPath) || Directory.Exists(rootedPath))
                        {
                            transformedItems.Add(item);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that combines the existing paths of the input items with a given relative path.
                /// </summary>
                internal static void Combine(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string relativePath = arguments[0];

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (String.IsNullOrEmpty(item.Key))
                        {
                            continue;
                        }

                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);
                        string combinedPath = Path.Combine(unescapedPath, relativePath);
                        string escapedPath = EscapingUtilities.Escape(combinedPath);
                        transformedItems.Add(new KeyValuePair<string, S>(escapedPath, null));
                    }
                }

                /// <summary>
                /// Intrinsic function that adds all ancestor directories of the given items.
                /// </summary>
                internal static void GetPathsOfAllDirectoriesAbove(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    // Phase 1: find all the applicable directories.

                    SortedSet<string> directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (String.IsNullOrEmpty(item.Key))
                        {
                            continue;
                        }

                        string directoryName = null;

                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

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
                                string baseDirectoryToUse = item.Value.ProjectDirectory ?? String.Empty;
                                rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                            }

                            // Normalize the path to remove elements like "..".
                            // Otherwise we run the risk of returning two or more different paths that represent the
                            // same directory.
                            rootedPath = FileUtilities.NormalizePath(rootedPath);
                            directoryName = Path.GetDirectoryName(rootedPath);
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                        }

                        while (!String.IsNullOrEmpty(directoryName))
                        {
                            if (directories.Contains(directoryName))
                            {
                                // We've already got this directory (and all its ancestors) in the set.
                                break;
                            }

                            directories.Add(directoryName);
                            directoryName = Path.GetDirectoryName(directoryName);
                        }
                    }

                    // Phase 2: Go through the directories and return them in order

                    foreach (string directoryPath in directories)
                    {
                        string escapedDirectoryPath = EscapingUtilities.Escape(directoryPath);
                        transformedItems.Add(new KeyValuePair<string, S>(escapedDirectoryPath, null));
                    }
                }

                /// <summary>
                /// Intrinsic function that adds the DirectoryName of the items in itemsOfType
                /// UNDONE: This can be removed in favor of a built-in %(DirectoryName) metadata in future.
                /// </summary>
                internal static void DirectoryName(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    Dictionary<string, string> directoryNameTable = new Dictionary<string, string>(itemsOfType.Count, StringComparer.OrdinalIgnoreCase);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        // If the item include has become empty,
                        // this is the end of the pipeline for this item
                        if (String.IsNullOrEmpty(item.Key))
                        {
                            continue;
                        }

                        string directoryName;
                        if (!directoryNameTable.TryGetValue(item.Key, out directoryName))
                        {
                            // Unescape as we are passing to the file system
                            string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

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
                                    string baseDirectoryToUse = item.Value.ProjectDirectory ?? String.Empty;
                                    rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                                }

                                directoryName = Path.GetDirectoryName(rootedPath);
                            }
                            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                            }

                            // Escape as this is going back into the engine
                            directoryName = EscapingUtilities.Escape(directoryName);
                            directoryNameTable[unescapedPath] = directoryName;
                        }

                        if (!String.IsNullOrEmpty(directoryName))
                        {
                            // return a result through the enumerator
                            transformedItems.Add(new KeyValuePair<string, S>(directoryName, item.Value));
                        }
                        else if (includeNullEntries)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds the contents of the metadata in specified in argument[0].
                /// </summary>
                internal static void Metadata(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string metadataName = arguments[0];

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (item.Value != null)
                        {
                            string metadataValue = null;

                            try
                            {
                                metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                            }
                            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                            {
                                // Blank metadata name
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }

                            if (!String.IsNullOrEmpty(metadataValue))
                            {
                                // It may be that the itemspec has unescaped ';'s in it so we need to split here to handle
                                // that case.
                                if (metadataValue.Contains(';'))
                                {
                                    var splits = ExpressionShredder.SplitSemiColonSeparatedList(metadataValue);

                                    foreach (string itemSpec in splits)
                                    {
                                        // return a result through the enumerator
                                        transformedItems.Add(new KeyValuePair<string, S>(itemSpec, item.Value));
                                    }
                                }
                                else
                                {
                                    // return a result through the enumerator
                                    transformedItems.Add(new KeyValuePair<string, S>(metadataValue, item.Value));
                                }
                            }
                            else if (metadataValue != String.Empty && includeNullEntries)
                            {
                                transformedItems.Add(new KeyValuePair<string, S>(metadataValue, item.Value));
                            }
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case sensitive comparison.
                /// </summary>
                internal static void DistinctWithCase(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    DistinctWithComparer(elementLocation, functionName, itemsOfType, arguments, StringComparer.Ordinal, transformedItems);
                }

                /// <summary>
                /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void Distinct(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    DistinctWithComparer(elementLocation, functionName, itemsOfType, arguments, StringComparer.OrdinalIgnoreCase, transformedItems);
                }

                /// <summary>
                /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void DistinctWithComparer(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, StringComparer comparer, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    // This dictionary will ensure that we only return one result per unique itemspec
                    HashSet<string> seenItems = new HashSet<string>(itemsOfType.Count, comparer);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (item.Key != null && seenItems.Add(item.Key))
                        {
                            transformedItems.Add(item);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function reverses the item list.
                /// </summary>
                internal static void Reverse(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    for (int i = itemsOfType.Count - 1; i >= 0; i--)
                    {
                        transformedItems.Add(itemsOfType[i]);
                    }
                }

                /// <summary>
                /// Intrinsic function that transforms expressions like the %(foo) in @(Compile->'%(foo)').
                /// </summary>
                internal static void ExpandQuotedExpressionFunction(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string quotedExpressionFunction = arguments[0];
                    OneOrMultipleMetadataMatches matches = GetQuotedExpressionMatches(quotedExpressionFunction, elementLocation);

                    // This is just a sanity check in case a code change causes something in the call stack to take this reference.
                    SpanBasedStringBuilder includeBuilder = s_includeBuilder ?? new SpanBasedStringBuilder();
                    s_includeBuilder = null;

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        string include = null;

                        // If we've been handed a null entry by an upstream transform
                        // then we don't want to try to tranform it with an itemspec modification.
                        // Simply allow the null to be passed along (if, we are including nulls as specified by includeNullEntries
                        if (item.Key != null)
                        {
                            int curIndex = 0;

                            switch (matches.Type)
                            {
                                case MetadataMatchType.None:
                                    // If we didn't match anything, just use the original string.
                                    include = quotedExpressionFunction;
                                    break;

                                // If we matched on a full string, we don't have to concatenate anything.
                                case MetadataMatchType.ExactSingle:
                                    include = GetMetadataValueFromMatch(matches.Single, item.Key, item.Value, elementLocation, ref curIndex);
                                    break;

                                // If we matched on a partial string, just replace the single group.
                                case MetadataMatchType.InexactSingle:
                                    includeBuilder.Append(quotedExpressionFunction, 0, matches.Single.Index);
                                    includeBuilder.Append(
                                        GetMetadataValueFromMatch(matches.Single, item.Key, item.Value, elementLocation, ref curIndex));
                                    includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                    include = includeBuilder.ToString();
                                    includeBuilder.Clear();
                                    break;

                                // Otherwise, iteratively replace each match group.
                                case MetadataMatchType.Multiple:
                                    foreach (MetadataMatch match in matches.Multiple)
                                    {
                                        includeBuilder.Append(quotedExpressionFunction, curIndex, match.Index - curIndex);
                                        includeBuilder.Append(
                                            GetMetadataValueFromMatch(match, item.Key, item.Value, elementLocation, ref curIndex));
                                    }

                                    includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                    include = includeBuilder.ToString();
                                    includeBuilder.Clear();
                                    break;
                                default:
                                    break;
                            }
                        }

                        // Include may be empty. Historically we have created items with empty include
                        // and ultimately set them on tasks, but we don't do that anymore as it's broken.
                        // Instead we optionally add a null, so that input and output lists are the same length; this allows
                        // the caller to possibly do correlation.

                        // We pass in the existing item so we can copy over its metadata
                        if (!string.IsNullOrEmpty(include))
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(include, item.Value));
                        }
                        else if (includeNullEntries)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                        }
                    }

                    s_includeBuilder = includeBuilder;
                }

                /// <summary>
                /// Extracts a value from the input string based on a regular expression.
                /// In the vast majority of cases, we'll only have 1-2 matches, and within those we can avoid allocating
                /// the vast majority of Regex objects and return a cached result.
                /// </summary>
                private static OneOrMultipleMetadataMatches GetQuotedExpressionMatches(string quotedExpressionFunction, IElementLocation elementLocation)
                {
                    // Start with fast paths to avoid any allocations.
                    if (TryGetCachedMetadataMatch(quotedExpressionFunction, out string cachedName)
                        || s_itemSpecModifiers.TryGetValue(quotedExpressionFunction, out cachedName))
                    {
                        return new OneOrMultipleMetadataMatches(cachedName);
                    }

                    // GroupCollection + Groups are the most expensive source of allocations here, so we want to return
                    // before ever accessing the property. Simply accessing it will trigger the full collection
                    // allocation, so we avoid it unless absolutely necessary.
                    // Unfortunately even .NET Core does not have a struct-based Group enumerator at this point.
                    Match match = RegularExpressions.ItemMetadataRegex.Match(quotedExpressionFunction);

                    if (!match.Success)
                    {
                        // No matches - the caller will use the original string.
                        return new OneOrMultipleMetadataMatches();
                    }

                    // From here will either return:
                    // 1. A single match, which may be offset within the input string..
                    // 2. A list of multiple matches.
                    List<MetadataMatch> multipleMatches = null;
                    while (match.Success)
                    {
                        // If true, this is likely an interpolated string, e.g. NETCOREAPP%(Identity)_OR_GREATER
                        bool isItemSpecModifier = s_itemSpecModifiers.TryGetValue(match.Value, out string name);
                        if (!isItemSpecModifier)
                        {
                            // Here is the worst case path which we've hopefully avoided at the point.
                            GroupCollection groupCollection = match.Groups;
                            name = groupCollection[RegularExpressions.NameGroup].Value;
                            ProjectErrorUtilities.VerifyThrowInvalidProject(groupCollection[RegularExpressions.ItemSpecificationGroup].Length == 0, elementLocation, "QualifiedMetadataInTransformNotAllowed", match.Value, name);
                        }

                        Match nextMatch = match.NextMatch();

                        // If we only have a single match, return before allocating the list.
                        bool isSingleMatch = multipleMatches == null && !nextMatch.Success;
                        if (isSingleMatch)
                        {
                            OneOrMultipleMetadataMatches singleMatch = new(quotedExpressionFunction, match, name);

                            // Only cache full string matches - skip known modifiers since they are permenantly cached.
                            if (singleMatch.Type == MetadataMatchType.ExactSingle && !isItemSpecModifier)
                            {
                                s_lastParsedQuotedExpression = name;
                            }

                            return singleMatch;
                        }

                        // We have multiple matches, so run the full loop.
                        // e.g. %(Filename)%(Extension)
                        // This is a very hot path, so we avoid allocating this until after we know there are multiple matches.
                        multipleMatches ??= [];
                        multipleMatches.Add(new MetadataMatch(match, name));
                        match = nextMatch;
                    }

                    return new OneOrMultipleMetadataMatches(multipleMatches);
                }

                /// <summary>
                /// Given a string such as %(ReferenceAssembly), check if the inner substring matches the cached value.
                /// If so, return the cached substring without allocating.
                /// </summary>
                /// <remarks>
                /// <see cref="ExpandQuotedExpressionFunction"/> often receives the same expression for multiple calls.
                /// To save on regex overhead, we cache the last substring extracted from a regex match.
                /// This is thread-safe as long as all checks work on a consistent local reference.
                /// </remarks>
                private static bool TryGetCachedMetadataMatch(string stringToCheck, out string cachedMatch)
                {
                    // Pull a local reference first in case the cached value is swapped.
                    cachedMatch = s_lastParsedQuotedExpression;
                    if (string.IsNullOrEmpty(cachedMatch))
                    {
                        return false;
                    }

                    // Quickly cancel out of definite misses.
                    int length = stringToCheck.Length;
                    if (length == cachedMatch.Length + QuotedExpressionSurroundCharCount
                        && stringToCheck[0] == '%' && stringToCheck[1] == '(' && stringToCheck[length - 1] == ')')
                    {
                        // If the inner slice is a hit, don't allocate a string.
                        ReadOnlySpan<char> span = stringToCheck.AsSpan(2, length - QuotedExpressionSurroundCharCount);
                        if (span.SequenceEqual(cachedMatch.AsSpan()))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                /// <summary>
                /// Intrinsic function that transforms expressions by invoking methods of System.String on the itemspec
                /// of the item in the pipeline.
                /// </summary>
                internal static void ExecuteStringFunction(
                    Expander<P, I> expander,
                    IElementLocation elementLocation,
                    bool includeNullEntries,
                    string functionName,
                    List<KeyValuePair<string, S>> itemsOfType,
                    string[] arguments,
                    List<KeyValuePair<string, S>> transformedItems)
                {
                    // Transform: expression is like @(Compile->'%(foo)'), so create completely new items,
                    // using the Include from the source items
                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        Function<P> function = new Function<P>(
                            typeof(string),
                            item.Key,
                            item.Key,
                            functionName,
                            arguments,
                            BindingFlags.Public | BindingFlags.InvokeMethod,
                            string.Empty,
                            expander.PropertiesUseTracker,
                            expander._fileSystem,
                            expander._loggingContext);

                        object result = function.Execute(item.Key, expander._properties, ExpanderOptions.ExpandAll, elementLocation);

                        string include = Expander<P, I>.PropertyExpander<P>.ConvertToString(result);

                        // We pass in the existing item so we can copy over its metadata
                        if (include.Length > 0)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(include, item.Value));
                        }
                        else if (includeNullEntries)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds the items from itemsOfType with their metadata cleared, i.e. only the itemspec is retained.
                /// </summary>
                internal static void ClearMetadata(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (includeNullEntries || item.Key != null)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(item.Key, null));
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds only those items that have a not-blank value for the metadata specified
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void HasMetadata(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string metadataName = arguments[0];

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        // GetMetadataValueEscaped returns empty string for missing metadata,
                        // but IItem specifies it should return null
                        if (!string.IsNullOrEmpty(metadataValue))
                        {
                            // return a result through the enumerator
                            transformedItems.Add(item);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds only those items have the given metadata value
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void WithMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string metadataName = arguments[0];
                    string metadataValueToFind = arguments[1];

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            // return a result through the enumerator
                            transformedItems.Add(item);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds those items don't have the given metadata value
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void WithoutMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string metadataName = arguments[0];
                    string metadataValueToFind = arguments[1];

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (!String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            // return a result through the enumerator
                            transformedItems.Add(item);
                        }
                    }
                }

                /// <summary>
                /// Intrinsic function that adds a boolean to indicate if any of the items have the given metadata value
                /// Using a case insensitive comparison.
                /// </summary>
                internal static void AnyHaveMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                    string metadataName = arguments[0];
                    string metadataValueToFind = arguments[1];
                    bool metadataFound = false;

                    foreach (KeyValuePair<string, S> item in itemsOfType)
                    {
                        if (item.Value != null)
                        {
                            string metadataValue = null;

                            try
                            {
                                metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                            }
                            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                            {
                                // Blank metadata name
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                            }

                            if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                            {
                                metadataFound = true;

                                // return a result through the enumerator
                                transformedItems.Add(new KeyValuePair<string, S>("true", item.Value));

                                // break out as soon as we found a match
                                return;
                            }
                        }
                    }

                    if (!metadataFound)
                    {
                        // We did not locate an item with the required metadata
                        transformedItems.Add(new KeyValuePair<string, S>("false", null));
                    }
                }

                /// <summary>
                /// Expands the metadata in the match provided into a string result.
                /// The match is expected to be the content of a transform.
                /// For example, representing "%(Filename.obj)" in the original expression "@(Compile->'%(Filename.obj)')".
                /// </summary>
                private static string GetMetadataValueFromMatch(
                    MetadataMatch match,
                    string itemSpec,
                    IItem sourceOfMetadata,
                    IElementLocation elementLocation,
                    ref int curIndex)
                {
                    string value = null;
                    try
                    {
                        if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(match.Name))
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case, we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            string directoryToUse = sourceOfMetadata.ProjectDirectory ?? Directory.GetCurrentDirectory();
                            string definingProjectEscaped = sourceOfMetadata.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);

                            value = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(directoryToUse, itemSpec, definingProjectEscaped, match.Name);
                        }
                        else
                        {
                            value = sourceOfMetadata.GetMetadataValueEscaped(match.Name);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", match.Name, ex.Message);
                    }

                    curIndex = match.Index + match.Length;
                    return value;
                }

                /// <summary>
                /// The type of match we found.
                /// We use this to determine how to build the final output string.
                /// </summary>
                private enum MetadataMatchType
                {

                    /// <summary>
                    /// No matches found. The result will be empty.
                    /// </summary>
                    None,

                    /// <summary>
                    /// An exact full string match, e.g. '%(FullPath)'.
                    /// </summary>
                    ExactSingle,

                    /// <summary>
                    /// A single match with surrounding characters, e.g. 'somedir/%(FileName)'.
                    /// </summary>
                    InexactSingle,

                    /// <summary>
                    /// Multiple matches found, e.g. '%(FullPath)%(Extension)'.
                    /// </summary>
                    Multiple,
                }

                /// <summary>
                /// A discriminated union between one exact, one partial, or multiple matches.
                /// </summary>
                private readonly struct OneOrMultipleMetadataMatches
                {
                    public OneOrMultipleMetadataMatches()
                    {
                        Type = MetadataMatchType.None;
                    }

                    public OneOrMultipleMetadataMatches(string name)
                    {
                        Type = MetadataMatchType.ExactSingle;
                        Single = new MetadataMatch(name);
                    }

                    public OneOrMultipleMetadataMatches(string quotedExpressionFunction, Match match, string name)
                    {
                        // We know we have a full string match when our extracted name is the same length as the input
                        // string minus the surrounding characters.
                        Type = quotedExpressionFunction.Length == name.Length + QuotedExpressionSurroundCharCount
                                ? MetadataMatchType.ExactSingle
                                : MetadataMatchType.InexactSingle;
                        Single = new MetadataMatch(match, name);
                    }

                    public OneOrMultipleMetadataMatches(List<MetadataMatch> allMatches)
                    {
                        Type = MetadataMatchType.Multiple;
                        Multiple = allMatches;
                    }

                    internal MetadataMatch Single { get; }

                    internal List<MetadataMatch> Multiple { get; }

                    internal MetadataMatchType Type { get; }
                }

                /// <summary>
                /// Represents a single match. Whether it was cached or from a Regex should be transparent
                /// since we simulate the length calculation.
                /// </summary>
                private readonly struct MetadataMatch
                {
                    public MetadataMatch(string name)
                    {
                        Name = name;
                        Index = 0;
                        Length = name.Length + QuotedExpressionSurroundCharCount;
                    }

                    public MetadataMatch(Match match, string name)
                    {
                        Name = name;
                        Index = match.Index;
                        Length = match.Length;
                    }

                    /// <summary>
                    /// The inner value of the match.
                    /// </summary>
                    internal string Name { get; }

                    /// <summary>
                    /// The index of the match in the original string.
                    /// If we have an exact string match, this will be 0.
                    /// </summary>
                    internal int Index { get; }

                    /// <summary>
                    /// The length of the match in the original string.
                    /// If we have an exact string match, this computed to match the original input.
                    /// </summary>
                    internal int Length { get; }
                }
            }
        }

        /// <summary>
        /// Regular expressions used by the expander.
        /// The expander currently uses regular expressions rather than a parser to do its work.
        /// </summary>
        private static partial class RegularExpressions
        {
            /**************************************************************************************************************************
            * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
            * description of an item vector changes, the expressions must be updated in both places.
            *************************************************************************************************************************/

#if NET
            [GeneratedRegex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
            internal static partial Regex ItemMetadataRegex { get; }
#else
            /// <summary>
            /// Regular expression used to match item metadata references embedded in strings.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary>
            internal static Regex ItemMetadataRegex => s_itemMetadataRegex ??=
                new Regex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

            internal static Regex s_itemMetadataRegex;
#endif

            /// <summary>
            /// Name of the group matching the "name" of a metadatum.
            /// </summary>
            internal const string NameGroup = "NAME";

            /// <summary>
            /// Name of the group matching the prefix on a metadata expression, for example "Compile." in "%(Compile.Object)".
            /// </summary>
            internal const string ItemSpecificationGroup = "ITEM_SPECIFICATION";

            /// <summary>
            /// Name of the group matching the item type in an item expression or metadata expression.
            /// </summary>
            internal const string ItemTypeGroup = "ITEM_TYPE";

            internal const string NonTransformItemMetadataSpecification = @"((?<=" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                                ItemVectorWithTransformRHS + @")) | ((?<!" + ItemVectorWithTransformLHS + @")" +
                                                                ItemMetadataSpecification + @"(?=" + ItemVectorWithTransformRHS + @")) | ((?<!" +
                                                                ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                                ItemVectorWithTransformRHS + @"))";

#if NET
            [GeneratedRegex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
            internal static partial Regex NonTransformItemMetadataRegex { get; }
#else
            /// <summary>
            /// regular expression used to match item metadata references outside of item vector transforms.
            /// </summary>
            /// <remarks>PERF WARNING: this Regex is complex and tends to run slowly.</remarks>
            private static Regex s_nonTransformItemMetadataPattern;

            internal static Regex NonTransformItemMetadataRegex => s_nonTransformItemMetadataPattern ??=
                new Regex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#endif

            /// <summary>
            /// Complete description of an item metadata reference, including the optional qualifying item type.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary>
            private const string ItemMetadataSpecification = @"%\(\s* (?<ITEM_SPECIFICATION>(?<ITEM_TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)? (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @") \s*\)";

            /// <summary>
            /// description of an item vector with a transform, left hand side.
            /// </summary>
            private const string ItemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";

            /// <summary>
            /// description of an item vector with a transform, right hand side.
            /// </summary>
            private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

            /**************************************************************************************************************************
             * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
             *************************************************************************************************************************/
        }

        private struct FunctionBuilder<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type of this function's receiver.
            /// </summary>
            public Type ReceiverType { get; set; }

            /// <summary>
            /// The name of the function.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The arguments for the function.
            /// </summary>
            public string[] Arguments { get; set; }

            /// <summary>
            /// The expression that this function is part of.
            /// </summary>
            public string Expression { get; set; }

            /// <summary>
            /// The property name that this function is applied on.
            /// </summary>
            public string Receiver { get; set; }

            /// <summary>
            /// The binding flags that will be used during invocation of this function.
            /// </summary>
            public BindingFlags BindingFlags { get; set; }

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted.
            /// </summary>
            public string Remainder { get; set; }

            public IFileSystem FileSystem { get; set; }

            public LoggingContext LoggingContext { get; set; }

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            public PropertiesUseTracker PropertiesUseTracker { get; set; }

            internal readonly Function<T> Build()
            {
                return new Function<T>(
                    ReceiverType,
                    Expression,
                    Receiver,
                    Name,
                    Arguments,
                    BindingFlags,
                    Remainder,
                    PropertiesUseTracker,
                    FileSystem,
                    LoggingContext);
            }
        }

        /// <summary>
        /// This class represents the function as extracted from an expression
        /// It is also responsible for executing the function.
        /// </summary>
        /// <typeparam name="T">Type of the properties used to expand the expression.</typeparam>
        internal class Function<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type of this function's receiver.
            /// </summary>
            private Type _receiverType;

            /// <summary>
            /// The name of the function.
            /// </summary>
            private readonly string _methodMethodName;

            /// <summary>
            /// The arguments for the function.
            /// </summary>
            private readonly string[] _arguments;

            /// <summary>
            /// The expression that this function is part of.
            /// </summary>
            private readonly string _expression;

            /// <summary>
            /// The property name that this function is applied on.
            /// </summary>
            private readonly string _receiver;

            /// <summary>
            /// The binding flags that will be used during invocation of this function.
            /// </summary>
            private BindingFlags _bindingFlags;

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted.
            /// </summary>
            private readonly string _remainder;

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            private PropertiesUseTracker _propertiesUseTracker;

            private readonly IFileSystem _fileSystem;

            private readonly LoggingContext _loggingContext;

            /// <summary>
            /// Construct a function that will be executed during property evaluation.
            /// </summary>
            internal Function(
                Type receiverType,
                string expression,
                string receiver,
                string methodName,
                string[] arguments,
                BindingFlags bindingFlags,
                string remainder,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem,
                LoggingContext loggingContext)
            {
                _methodMethodName = methodName;
                if (arguments == null)
                {
                    _arguments = [];
                }
                else
                {
                    _arguments = arguments;
                }

                _receiver = receiver;
                _expression = expression;
                _receiverType = receiverType;
                _bindingFlags = bindingFlags;
                _remainder = remainder;
                _propertiesUseTracker = propertiesUseTracker;
                _fileSystem = fileSystem;
                _loggingContext = loggingContext;
            }

            /// <summary>
            /// Part of the extraction may result in the name of the property
            /// This accessor is used by the Expander
            /// Examples of expression root:
            ///     [System.Diagnostics.Process]::Start
            ///     SomeMSBuildProperty.
            /// </summary>
            internal string Receiver
            {
                get { return _receiver; }
            }

            /// <summary>
            /// Extract the function details from the given property function expression.
            /// </summary>
            internal static Function<T> ExtractPropertyFunction(
                string expressionFunction,
                IElementLocation elementLocation,
                object propertyValue,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem,
                LoggingContext loggingContext)
            {
                // Used to aggregate all the components needed for a Function
                FunctionBuilder<T> functionBuilder = new FunctionBuilder<T> { FileSystem = fileSystem, LoggingContext = loggingContext };

                // By default the expression root is the whole function expression
                ReadOnlySpan<char> expressionRoot = expressionFunction == null ? ReadOnlySpan<char>.Empty : expressionFunction.AsSpan();

                // The arguments for this function start at the first '('
                // If there are no arguments, then we're a property getter
                var argumentStartIndex = expressionFunction.IndexOf('(');

                // If we have arguments, then we only want the content up to but not including the '('
                if (argumentStartIndex > -1)
                {
                    expressionRoot = expressionRoot.Slice(0, argumentStartIndex);
                }

                // In case we ended up with something we don't understand
                ProjectErrorUtilities.VerifyThrowInvalidProject(!expressionRoot.IsEmpty, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                functionBuilder.Expression = expressionFunction;
                functionBuilder.PropertiesUseTracker = propertiesUseTracker;

                // This is a static method call
                // A static method is the content that follows the last "::", the rest being the type
                if (propertyValue == null && expressionRoot[0] == '[')
                {
                    var typeEndIndex = expressionRoot.IndexOf(']');

                    if (typeEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                    }

                    var typeName = Strings.WeakIntern(expressionRoot.Slice(1, typeEndIndex - 1));
                    var methodStartIndex = typeEndIndex + 1;

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

                    ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);

                    // Locate a type that matches the body of the expression.
                    var receiverType = GetTypeForStaticMethod(typeName, functionBuilder.Name);

                    if (receiverType == null)
                    {
                        // We ended up with something other than a type
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionTypeUnavailable", expressionFunction, typeName);
                    }

                    functionBuilder.ReceiverType = receiverType;
                }
                else if (expressionFunction[0] == '[') // We have an indexer
                {
                    var indexerEndIndex = expressionFunction.IndexOf(']', 1);
                    if (indexerEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }

                    var methodStartIndex = indexerEndIndex + 1;

                    functionBuilder.ReceiverType = propertyValue.GetType();

                    ConstructIndexerFunction(expressionFunction, elementLocation, propertyValue, methodStartIndex, indexerEndIndex, ref functionBuilder);
                }
                else // This could be a property reference, or a chain of function calls
                {
                    // Look for an instance function call next, such as in SomeStuff.ToLower()
                    var methodStartIndex = expressionRoot.IndexOf('.');
                    if (methodStartIndex == -1)
                    {
                        // We don't have a function invocation in the expression root, return null
                        return null;
                    }

                    // skip over the '.';
                    methodStartIndex++;

                    var rootEndIndex = expressionRoot.IndexOf('.');

                    // If this is an instance function rather than a static, then we'll capture the name of the property referenced
                    var functionReceiver = Strings.WeakIntern(expressionRoot.Slice(0, rootEndIndex).Trim());

                    // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                    if (propertyValue == null && !IsValidPropertyName(functionReceiver))
                    {
                        // We extracted something that wasn't a valid property name, fail.
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    }

                    // If we are recursively acting on a type that has been already produced then pass that type inwards (e.g. we are interpreting a function call chain)
                    // Otherwise, the receiver of the function is a string
                    var receiverType = propertyValue?.GetType() ?? typeof(string);

                    functionBuilder.Receiver = functionReceiver;
                    functionBuilder.ReceiverType = receiverType;

                    ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);
                }

                return functionBuilder.Build();
            }

            /// <summary>
            /// Execute the function on the given instance.
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
                        if (!IsStaticMethodAvailable(_receiverType, _methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Static;

                        // For our intrinsic function we need to support calling of internal methods
                        // since we don't want them to be public
                        if (_receiverType == typeof(IntrinsicFunctions))
                        {
                            _bindingFlags |= BindingFlags.NonPublic;
                        }
                    }
                    else
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsInstanceMethodAvailable(_methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Instance;

                        // The object that we're about to call methods on may have escaped characters
                        // in it, we want to operate on the unescaped string in the function, just as we
                        // want to pass arguments that are unescaped (see below)
                        if (objectInstance is string objectInstanceString)
                        {
                            objectInstance = EscapingUtilities.UnescapeAll(objectInstanceString);
                        }
                    }

                    // We have a methodinfo match, need to plug in the arguments
                    args = new object[_arguments.Length];

                    // Assemble our arguments ready for passing to our method
                    for (int n = 0; n < _arguments.Length; n++)
                    {
                        object argument = PropertyExpander<T>.ExpandPropertiesLeaveTypedAndEscaped(
                            _arguments[n],
                            properties,
                            options,
                            elementLocation,
                            _propertiesUseTracker,
                            _fileSystem);

                        if (argument is string argumentValue)
                        {
                            // Unescape the value since we're about to send it out of the engine and into
                            // the function being called. If a file or a directory function, fix the path
                            if (_receiverType == typeof(File) || _receiverType == typeof(Directory)
                                || _receiverType == typeof(Path))
                            {
                                argumentValue = FileUtilities.FixFilePath(argumentValue);
                            }

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
                    if (objectInstance != null && args.Length == 1 && (String.Equals("Equals", _methodMethodName, StringComparison.OrdinalIgnoreCase) || String.Equals("CompareTo", _methodMethodName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Support comparison when the lhs is an integer
                        if (ParseArgs.IsFloatingPointRepresentation(args[0]))
                        {
                            if (double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double result))
                            {
                                objectInstance = result;
                                _receiverType = objectInstance.GetType();
                            }
                        }

                        // change the type of the final unescaped string into the destination
                        args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                    }

                    if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        // Special case a few methods that take extra parameters that can't be passed in by the user
                        if (_methodMethodName.Equals("GetPathOfFileAbove") && args.Length == 1)
                        {
                            // Append the IElementLocation as a parameter to GetPathOfFileAbove if the user only
                            // specified the file name.  This is syntactic sugar so they don't have to always
                            // include $(MSBuildThisFileDirectory) as a parameter.
                            string startingDirectory = String.IsNullOrWhiteSpace(elementLocation.File) ? String.Empty : Path.GetDirectoryName(elementLocation.File);

                            args = [args[0], startingDirectory];
                        }
                    }

                    // If we've been asked to construct an instance, then we
                    // need to locate an appropriate constructor and invoke it
                    if (String.Equals("new", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(_receiverType, out functionResult, args))
                        {
                            functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                        }
                    }
                    else
                    {
                        bool wellKnownFunctionSuccess = false;

                        try
                        {
                            // First attempt to recognize some well-known functions to avoid binding
                            // and potential first-chance MissingMethodExceptions.
                            wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunction(_methodMethodName, _receiverType, _fileSystem, out functionResult, objectInstance, args);

                            if (!wellKnownFunctionSuccess)
                            {
                                // Some well-known functions need evaluated value from properties.
                                wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(_methodMethodName, _receiverType, _loggingContext, properties, out functionResult, objectInstance, args);
                            }
                        }
                        // we need to preserve the same behavior on exceptions as the actual binder
                        catch (Exception ex)
                        {
                            string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                            if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                            {
                                return partiallyEvaluated;
                            }

                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message.Replace("\r\n", " "));
                        }

                        if (!wellKnownFunctionSuccess)
                        {
                            // Execute the function given converted arguments
                            // The only exception that we should catch to try a late bind here is missing method
                            // otherwise there is the potential of running a function twice!
                            try
                            {
                                // If there are any out parameters, try to figure out their type and create defaults for them as appropriate before calling the method.
                                if (args.Any(a => "out _".Equals(a)))
                                {
                                    IEnumerable<MethodInfo> methods = _receiverType.GetMethods(_bindingFlags).Where(m => m.Name.Equals(_methodMethodName) && m.GetParameters().Length == args.Length);
                                    functionResult = GetMethodResult(objectInstance, methods, args, 0);
                                }
                                else
                                {
                                    // If there are no out parameters, use InvokeMember using the standard binder - this will match and coerce as needed
                                    functionResult = _receiverType.InvokeMember(_methodMethodName, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture);
                                }
                            }
                            // If we're invoking a method, then there are deeper attempts that can be made to invoke the method.
                            // If not, we were asked to get a property or field but found that we cannot locate it. No further argument coercion is possible, so throw.
                            catch (MissingMethodException ex) when ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                            {
                                // The standard binder failed, so do our best to coerce types into the arguments for the function
                                // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true.
                                functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, false /* is not constructor */);
                            }
                        }
                    }

                    // If the result of the function call is a string, then we need to escape the result
                    // so that we maintain the "engine contains escaped data" state.
                    // The exception is that the user is explicitly calling MSBuild::Unescape, MSBuild::Escape, or ConvertFromBase64
                    if (functionResult is string functionResultString &&
                        !String.Equals("Unescape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals("Escape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals("ConvertFromBase64", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = EscapingUtilities.Escape(functionResultString);
                    }

                    // We have nothing left to parse, so we'll return what we have
                    if (String.IsNullOrEmpty(_remainder))
                    {
                        return functionResult;
                    }

                    // Recursively expand the remaining property body after execution
                    return PropertyExpander<T>.ExpandPropertyBody(
                        _remainder,
                        functionResult,
                        properties,
                        options,
                        elementLocation,
                        _propertiesUseTracker,
                        _fileSystem);
                }

                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
                catch (TargetInvocationException ex)
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                    if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                    {
                        // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                        return partiallyEvaluated;
                    }
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                    return null;
                }

                // Any other exception was thrown by trying to call it
                catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
                {
                    // If there's a :: in the expression, they were probably trying for a static function
                    // invocation. Give them some more relevant info in that case
                    if (s_invariantCompareInfo.IndexOf(_expression, "::", CompareOptions.OrdinalIgnoreCase) > -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", _expression, ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                    }
                    else
                    {
                        // We ended up with something other than a function expression
                        string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                    }

                    return null;
                }
            }

            private object GetMethodResult(object objectInstance, IEnumerable<MethodInfo> methods, object[] args, int index)
            {
                for (int i = index; i < args.Length; i++)
                {
                    if (args[i].Equals("out _"))
                    {
                        object toReturn = null;
                        foreach (MethodInfo method in methods)
                        {
                            Type t = method.GetParameters()[i].ParameterType;
                            args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                            object currentReturnValue = GetMethodResult(objectInstance, methods, args, i + 1);
                            if (currentReturnValue is not null)
                            {
                                if (toReturn is null)
                                {
                                    toReturn = currentReturnValue;
                                }
                                else if (!toReturn.Equals(currentReturnValue))
                                {
                                    // There were multiple methods that seemed viable and gave different results. We can't differentiate between them so throw.
                                    ErrorUtilities.ThrowArgument("CouldNotDifferentiateBetweenCompatibleMethods", _methodMethodName, args.Length);
                                    return null;
                                }
                            }
                        }

                        return toReturn;
                    }
                }

                try
                {
                    return _receiverType.InvokeMember(_methodMethodName, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture) ?? "null";
                }
                catch (Exception)
                {
                    // This isn't a viable option, but perhaps another set of parameters will work.
                    return null;
                }
            }

            /// <summary>
            /// Given a type name and method name, try to resolve the type.
            /// </summary>
            /// <param name="typeName">May be full name or assembly qualified name.</param>
            /// <param name="simpleMethodName">simple name of the method.</param>
            /// <returns></returns>
            private static Type GetTypeForStaticMethod(string typeName, string simpleMethodName)
            {
                Type receiverType;
                Tuple<string, Type> cachedTypeInformation;

                // If we don't have a type name, we already know that we won't be able to find a type.
                // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                // Check if the type is in the allowlist cache. If it is, use it or load it.
                cachedTypeInformation = AvailableStaticMethods.GetTypeInformationFromTypeCache(typeName, simpleMethodName);
                if (cachedTypeInformation != null)
                {
                    // We need at least one of these set
                    ErrorUtilities.VerifyThrow(cachedTypeInformation.Item1 != null || cachedTypeInformation.Item2 != null, "Function type information needs either string or type represented.");

                    // If we have the type information in Type form, then just return that
                    if (cachedTypeInformation.Item2 != null)
                    {
                        return cachedTypeInformation.Item2;
                    }
                    else if (cachedTypeInformation.Item1 != null)
                    {
                        // This is a case where the Type is not available at compile time, so
                        // we are forced to bind by name instead
                        var assemblyQualifiedTypeName = cachedTypeInformation.Item1;

                        // Get the type from the assembly qualified type name from AvailableStaticMethods
                        receiverType = Type.GetType(assemblyQualifiedTypeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                        // If the type information from the cache is not loadable, it means the cache information got corrupted somehow
                        // Throw here to prevent adding null types in the cache
                        ErrorUtilities.VerifyThrowInternalNull(receiverType, $"Type information for {typeName} was present in the allowlist cache as {assemblyQualifiedTypeName} but the type could not be loaded.");

                        // If we've used it once, chances are that we'll be using it again
                        // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                        AvailableStaticMethods.TryAdd(typeName, simpleMethodName, new Tuple<string, Type>(assemblyQualifiedTypeName, receiverType));

                        return receiverType;
                    }
                }

                // Get the type from mscorlib (or the currently running assembly)
                receiverType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                if (receiverType != null)
                {
                    // DO NOT CACHE THE TYPE HERE!
                    // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
                    // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                    // Caching it here would load any type into the allow list.
                    return receiverType;
                }

                // Note the following code path is only entered when MSBUILDENABLEALLPROPERTYFUNCTIONS == 1.
                // This environment variable must not be cached - it should be dynamically settable while the application is executing.
                if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                {
                    // We didn't find the type, so go probing. First in System
                    receiverType = GetTypeFromAssembly(typeName, "System");

                    // Next in System.Core
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssembly(typeName, "System.Core");
                    }

                    // We didn't find the type, so try to find it using the namespace
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssemblyUsingNamespace(typeName);
                    }

                    if (receiverType != null)
                    {
                        // If we've used it once, chances are that we'll be using it again
                        // We can cache the type here, since all functions are enabled
                        AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, receiverType));
                    }
                }

                return receiverType;
            }

            /// <summary>
            /// Gets the specified type using the namespace to guess the assembly that its in.
            /// </summary>
            private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
            {
                string baseName = typeName;
                int assemblyNameEnd = baseName.Length;

                // If the string has no dot, or is nothing but a dot, we have no
                // namespace to look for, so we can't help.
                if (assemblyNameEnd <= 0)
                {
                    return null;
                }

                // We will work our way up the namespace looking for an assembly that matches
                while (assemblyNameEnd > 0)
                {
                    string candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                    // Try to load the assembly with the computed name
                    Type foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

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
            /// Get the specified type from the assembly partial name supplied.
            /// </summary>
            [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
            private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
            {
                Type objectType = null;

                // Try to load the assembly with the computed name
#if FEATURE_GAC
#pragma warning disable 618, 612
                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                // Assembly.Load requires the full assembly name to be passed to it.
                // Therefore we must ignore the deprecated warning.
                Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618, 612
#else
                Assembly candidateAssembly = null;
                try
                {
                    candidateAssembly = Assembly.Load(new AssemblyName(candidateAssemblyName));
                }
                catch (FileNotFoundException)
                {
                    // Swallow the error; LoadWithPartialName returned null when the partial name
                    // was not found but Load throws.  Either way we'll provide a nice "couldn't
                    // resolve this" error later.
                }
#endif

                if (candidateAssembly != null)
                {
                    objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
                }

                return objectType;
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for an indexer
            /// Also extracts the remainder of the expression that is not part of this indexer.
            /// </summary>
            private static void ConstructIndexerFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, int methodStartIndex, int indexerEndIndex, ref FunctionBuilder<T> functionBuilder)
            {
                ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(1, indexerEndIndex - 1);
                string[] functionArguments;

                // If there are no arguments, then just create an empty array
                if (argumentsContent.IsEmpty)
                {
                    functionArguments = [];
                }
                else
                {
                    // We will keep empty entries so that we can treat them as null
                    functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                }

                // choose the name of the function based on the type of the object that we
                // are using.
                string functionName;
                if (propertyValue is Array)
                {
                    functionName = "GetValue";
                }
                else if (propertyValue is string)
                {
                    functionName = "get_Chars";
                }
                else // a regular indexer
                {
                    functionName = "get_Item";
                }

                functionBuilder.Name = functionName;
                functionBuilder.Arguments = functionArguments;
                functionBuilder.BindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
                functionBuilder.Remainder = expressionFunction.Substring(methodStartIndex);
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for a static or instance function.
            /// Also extracts the remainder of the expression that is not part of this function.
            /// </summary>
            private static void ConstructFunction(IElementLocation elementLocation, string expressionFunction, int argumentStartIndex, int methodStartIndex, ref FunctionBuilder<T> functionBuilder)
            {
                // The unevaluated and unexpanded arguments for this function
                string[] functionArguments;

                // The name of the function that will be invoked
                ReadOnlySpan<char> functionName;

                // What's left of the expression once the function has been constructed
                ReadOnlySpan<char> remainder = ReadOnlySpan<char>.Empty;

                // The binding flags that we will use for this function's execution
                BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

                ReadOnlySpan<char> expressionFunctionAsSpan = expressionFunction.AsSpan();

                ReadOnlySpan<char> expressionSubstringAsSpan = argumentStartIndex > -1 ? expressionFunctionAsSpan.Slice(methodStartIndex, argumentStartIndex - methodStartIndex) : ReadOnlySpan<char>.Empty;

                // There are arguments that need to be passed to the function
                if (argumentStartIndex > -1 && !expressionSubstringAsSpan.Contains(".".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // separate the function and the arguments
                    functionName = expressionSubstringAsSpan.Trim();

                    // Skip the '('
                    argumentStartIndex++;

                    // Scan for the matching closing bracket, skipping any nested ones
                    int argumentsEndIndex = ScanForClosingParenthesis(expressionFunctionAsSpan, argumentStartIndex, out _, out _);

                    if (argumentsEndIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                    }

                    // We have been asked for a method invocation
                    defaultBindingFlags |= BindingFlags.InvokeMethod;

                    // It may be that there are '()' but no actual arguments content
                    if (argumentStartIndex == expressionFunction.Length - 1)
                    {
                        functionArguments = [];
                    }
                    else
                    {
                        // we have content within the '()' so let's extract and deal with it
                        ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                        // If there are no arguments, then just create an empty array
                        if (argumentsContent.IsEmpty)
                        {
                            functionArguments = [];
                        }
                        else
                        {
                            // We will keep empty entries so that we can treat them as null
                            functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                        }

                        remainder = expressionFunctionAsSpan.Slice(argumentsEndIndex + 1).Trim();
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

                    functionArguments = [];

                    if (nextMethodIndex > 0)
                    {
                        methodLength = nextMethodIndex - methodStartIndex;
                        remainder = expressionFunctionAsSpan.Slice(nextMethodIndex).Trim();
                    }

                    ReadOnlySpan<char> netPropertyName = expressionFunctionAsSpan.Slice(methodStartIndex, methodLength).Trim();

                    ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                    // We have been asked for a property or a field
                    defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                    functionName = netPropertyName;
                }

                // either there are no functions left or what we have is another function or an indexer
                if (remainder.IsEmpty || remainder[0] == '.' || remainder[0] == '[')
                {
                    functionBuilder.Name = functionName.ToString();
                    functionBuilder.Arguments = functionArguments;
                    functionBuilder.BindingFlags = defaultBindingFlags;
                    functionBuilder.Remainder = remainder.ToString();
                }
                else
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                }
            }

            /// <summary>
            /// Coerce the arguments according to the parameter types
            /// Will only return null if the coercion didn't work due to an InvalidCastException.
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
                        else if (parameters[n].ParameterType.GetTypeInfo().IsEnum && args[n] is string v && v.Contains('.'))
                        {
                            Type enumType = parameters[n].ParameterType;
                            string typeLeafName = $"{enumType.Name}.";
                            string typeFullName = $"{enumType.FullName}.";

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
                // The coercion failed therefore we return null
                catch (InvalidCastException)
                {
                    return null;
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (OverflowException)
                {
                    // https://github.com/dotnet/msbuild/issues/2882
                    // test: PropertyFunctionMathMaxOverflow
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
                    string typeName = _receiverType.FullName;

                    // We don't want to expose the real type name of our intrinsics
                    // so we'll replace it with "MSBuild"
                    if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        typeName = "MSBuild";
                    }
                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return $"[{typeName}]::{name}({parameters})";
                    }
                    else
                    {
                        return $"[{typeName}]::{name}";
                    }
                }
                else
                {
                    string propertyValue = $"\"{objectInstance as string}\"";

                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return $"{propertyValue}.{name}({parameters})";
                    }
                    else
                    {
                        return $"{propertyValue}.{name}";
                    }
                }
            }

            /// <summary>
            /// Check the property function allowlist whether this method is available.
            /// </summary>
            private static bool IsStaticMethodAvailable(Type receiverType, string methodName)
            {
                if (receiverType == typeof(IntrinsicFunctions))
                {
                    // These are our intrinsic functions, so we're OK with those
                    return true;
                }

                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                return AvailableStaticMethods.GetTypeInformationFromTypeCache(receiverType.FullName, methodName) != null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsInstanceMethodAvailable(string methodName)
            {
                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                // This could be expanded to an allow / deny list.
                return !string.Equals("GetType", methodName, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Construct and instance of objectType based on the constructor or method arguments provided.
            /// Arguments must never be null.
            /// </summary>
            private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
            {
                // First let's try for a method where all arguments are strings..
                Type[] types = new Type[_arguments.Length];
                for (int n = 0; n < _arguments.Length; n++)
                {
                    types[n] = typeof(string);
                }

                MethodBase memberInfo;
                if (isConstructor)
                {
                    memberInfo = _receiverType.GetConstructor(bindingFlags, null, types, null);
                }
                else
                {
                    memberInfo = _receiverType.GetMethod(_methodMethodName, bindingFlags, null, types, null);
                }

                // If we didn't get a match on all string arguments,
                // search for a method with the right number of arguments
                if (memberInfo == null)
                {
                    // Gather all methods that may match
                    IEnumerable<MethodBase> members;
                    if (isConstructor)
                    {
                        members = _receiverType.GetConstructors(bindingFlags);
                    }
                    else if (_receiverType == typeof(IntrinsicFunctions) && IntrinsicFunctionOverload.IsKnownOverloadMethodName(_methodMethodName))
                    {
                        MemberInfo[] foundMembers = _receiverType.FindMembers(
                            MemberTypes.Method,
                            bindingFlags,
                            (info, criteria) => string.Equals(info.Name, (string)criteria, StringComparison.OrdinalIgnoreCase),
                            _methodMethodName);
                        Array.Sort(foundMembers, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
                        members = foundMembers.Cast<MethodBase>();
                    }
                    else
                    {
                        members = _receiverType.GetMethods(bindingFlags).Where(m => string.Equals(m.Name, _methodMethodName, StringComparison.OrdinalIgnoreCase));
                    }

                    foreach (MethodBase member in members)
                    {
                        ParameterInfo[] parameters = member.GetParameters();

                        // Simple match on name and number of params, we will be case insensitive
                        if (parameters.Length == _arguments.Length)
                        {
                            // Try to find a method with the right name, number of arguments and
                            // compatible argument types
                            // we have a match on the name and argument number
                            // now let's try to coerce the arguments we have
                            // into the arguments on the matching method
                            object[] coercedArguments = CoerceArguments(args, parameters);

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

#nullable enable

    internal static class IntrinsicFunctionOverload
    {
        private static readonly string[] s_knownOverloadName = { "Add", "Subtract", "Multiply", "Divide", "Modulo", };

        // Order by the TypeCode of the first parameter.
        // When change wave is enabled, order long before double.
        // Otherwise preserve prior behavior of double before long.
        // For reuse, the comparer is cached in a non-generic type.
        // Both comparer instances can be cached to support change wave testing.
        private static IComparer<MemberInfo>? s_comparerLongBeforeDouble;

        internal static IComparer<MemberInfo> IntrinsicFunctionOverloadMethodComparer => LongBeforeDoubleComparer;

        private static IComparer<MemberInfo> LongBeforeDoubleComparer => s_comparerLongBeforeDouble ??= Comparer<MemberInfo>.Create((key0, key1) => SelectTypeOfFirstParameter(key0).CompareTo(SelectTypeOfFirstParameter(key1)));

        internal static bool IsKnownOverloadMethodName(string methodName) => s_knownOverloadName.Any(name => string.Equals(name, methodName, StringComparison.OrdinalIgnoreCase));

        private static TypeCode SelectTypeOfFirstParameter(MemberInfo member)
        {
            MethodBase? method = member as MethodBase;
            if (method == null)
            {
                return TypeCode.Empty;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length > 0
                ? Type.GetTypeCode(parameters[0].ParameterType)
                : TypeCode.Empty;
        }
    }
}
