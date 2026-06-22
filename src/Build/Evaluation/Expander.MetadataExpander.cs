// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    /// <summary>
    ///  Expands bare metadata expressions, like %(Compile.WarningLevel), or unqualified, like %(Compile).
    /// </summary>
    /// <remarks>
    ///  This is a private nested ref struct, exposed only through the static
    ///  <see cref="ExpandMetadataLeaveEscaped"/> entry point.
    /// </remarks>
    private readonly ref struct MetadataExpander
    {
        private readonly IMetadataTable _metadata;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly LoggingContext? _loggingContext;
        private readonly SpanBasedStringBuilder _builder;

        private MetadataExpander(
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext,
            SpanBasedStringBuilder builder)
        {
            _metadata = metadata;
            _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
            _elementLocation = elementLocation;
            _loggingContext = loggingContext;
            _builder = builder;
        }

        /// <summary>
        ///  Expands all embedded item metadata in the given string, using the bucketed items.
        ///  Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile).
        /// </summary>
        /// <param name="expression">The expression containing item metadata references.</param>
        /// <param name="metadata">The metadata to be expanded.</param>
        /// <param name="options">Used to specify what to expand.</param>
        /// <param name="elementLocation">The location information for error reporting purposes.</param>
        /// <param name="loggingContext">The logging context for this operation.</param>
        /// <returns>
        ///  The string with item metadata expanded in-place, escaped.
        /// </returns>
        internal static string ExpandMetadataLeaveEscaped(
            string expression,
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext = null)
        {
            if ((options & ExpanderOptions.ExpandMetadata) == 0)
            {
                return expression;
            }

            Assumed.NotNull(metadata, "Cannot expand metadata without providing metadata");

            // PERF NOTE: pre-scanning the string for "%(" is cheaper than a full scan.
            if (expression.IndexOf("%(", StringComparison.Ordinal) < 0)
            {
                return expression;
            }

            try
            {
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                MetadataExpander expander = new(metadata, options, elementLocation, loggingContext, builder);

                return expander.Expand(expression);
            }
            catch (InvalidOperationException ex)
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression, ex.Message);
            }

            return Assumed.Unreachable<string>();
        }

        private string Expand(string expression)
        {
            if (expression.IndexOf("@(", StringComparison.Ordinal) < 0)
            {
                // No item vectors in the string — scan for metadata references directly.
                ScanAndExpandMetadata(expression);
            }
            else
            {
                ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

                if (!enumerator.MoveNext())
                {
                    // The string contains "@(" but no well-formed item vector expressions —
                    // scan the entire string for metadata references.
                    ScanAndExpandMetadata(expression);
                }
                else if (enumerator.Current.Value == expression
                    && enumerator.Current.Separator == null
                    && !enumerator.MoveNext())
                {
                    // The entire expression is a single item vector with no separator, so there are
                    // no gaps to expand metadata in — return the expression unchanged.
                    return expression;
                }
                else
                {
                    // Reuse the already-advanced enumerator (positioned at the first capture) to
                    // expand metadata in the gaps between item vector expressions. This avoids
                    // shredding the expression a second time.
                    ScanAndExpandMetadataInGaps(expression, ref enumerator);
                }
            }

            return _builder.Equals(expression.AsSpan())
                ? expression
                : _builder.ToString();
        }

        /// <summary>
        ///  Expands metadata in the gaps between item vector expressions and within their separators.
        /// </summary>
        /// <remarks>
        ///  The supplied enumerator must already be positioned at the first capture (i.e. a successful
        ///  <see cref="ExpressionShredder.ReferencedItemExpressionsEnumerator.MoveNext"/> has been called).
        ///  Passing the already-advanced enumerator lets the caller's shred be reused instead of
        ///  re-scanning the expression from scratch.
        /// </remarks>
        private void ScanAndExpandMetadataInGaps(string expression, ref ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator)
        {
            int start = 0;

            do
            {
                start = ProcessItemExpressionCapture(expression, start, enumerator.Current);
            }
            while (enumerator.MoveNext());

            // Expand metadata in any trailing text after the last item vector expression.
            if (start < expression.Length)
            {
                ScanAndExpandMetadata(expression, start, expression.Length);
            }
        }

        private int ProcessItemExpressionCapture(string expression, int start, ExpressionShredder.ItemExpressionCapture itemExpressionCapture)
        {
            // Expand metadata in the gap before this item vector expression.
            if (itemExpressionCapture.Index > start)
            {
                ScanAndExpandMetadata(expression, start, itemExpressionCapture.Index);
            }

            // Expand metadata that appears in the item vector expression's separator.
            if (itemExpressionCapture.Separator != null)
            {
                // Append the portion before the separator verbatim, then expand within the separator portion.
                string value = itemExpressionCapture.Value;
                int separatorStart = itemExpressionCapture.SeparatorStart;

                _builder.Append(value, 0, separatorStart);
                ScanAndExpandMetadata(value, separatorStart, value.Length);
            }
            else
            {
                // Append the item vector expression as-is.
                _builder.Append(itemExpressionCapture.Value);
            }

            // Advance past this item vector expression.
            return itemExpressionCapture.Index + itemExpressionCapture.Length;
        }

        /// <inheritdoc cref="ScanAndExpandMetadata(string, int, int)" />
        private void ScanAndExpandMetadata(string input)
            => ScanAndExpandMetadata(input, 0, input.Length);

        /// <summary>
        ///  Scans the specified range of <paramref name="input"/> for item metadata references
        ///  of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>, expands them using the
        ///  provided metadata table, and appends the results to the builder.
        /// </summary>
        /// <remarks>
        ///  A valid metadata name starts with a letter or underscore, followed by zero or more
        ///  letters, digits, underscores, or hyphens: <c>[A-Za-z_][A-Za-z_0-9\-]*</c>. This grammar is
        ///  enforced by <see cref="ExpressionShredder.TryParseMetadataExpression"/> and must stay in sync
        ///  with <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>.
        ///  Whitespace is allowed around the parentheses and the dot separator.
        ///  If a <c>%(</c> sequence does not form a valid metadata reference, it is appended
        ///  to the output verbatim.
        /// </remarks>
        private void ScanAndExpandMetadata(string input, int startIndex, int endIndex)
        {
            int lastCopied = startIndex;

            for (int i = startIndex; i < endIndex - 1; i++)
            {
                if (input[i] != '%' || input[i + 1] != '(')
                {
                    continue;
                }

                int pos = i + 2;

                if (!ExpressionShredder.TryParseMetadataExpression(input, ref pos, endIndex, out string itemType, out string metadataName))
                {
                    // Not a valid metadata reference — skip past '%(' and continue scanning.
                    i++;
                    continue;
                }

                // Append everything before this reference.
                if (i > lastCopied)
                {
                    _builder.Append(input, lastCopied, i - lastCopied);
                }

                // Determine whether to expand this metadata reference.
                bool isBuiltInMetadata = ItemSpecModifiers.IsItemSpecModifier(metadataName);

                if ((isBuiltInMetadata && ((_options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
                   (!isBuiltInMetadata && ((_options & ExpanderOptions.ExpandCustomMetadata) != 0)))
                {
                    string expanded = _metadata.GetEscapedValue(itemType, metadataName);

                    if ((_options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 &&
                        _loggingContext != null &&
                        !string.IsNullOrEmpty(metadataName) &&
                        _metadata is IItemTypeDefinition itemMetadata &&
                        (string.IsNullOrEmpty(itemType) || string.Equals(itemType, itemMetadata.ItemType, StringComparison.Ordinal)))
                    {
                        _loggingContext.LogComment(
                            MessageImportance.Low,
                            new BuildEventFileInfo(_elementLocation),
                            "ItemReferencingSelfInTarget",
                            itemMetadata.ItemType,
                            metadataName);
                    }

                    if (IsTruncationEnabled(_options) && expanded.Length > CharacterLimitPerExpansion)
                    {
                        expanded = TruncateString(expanded);
                    }

                    _builder.Append(expanded);
                }
                else
                {
                    _builder.Append(input, i, pos - i);
                }

                lastCopied = pos;

                // Advance i to just before pos so the for-loop increment puts us at pos.
                i = pos - 1;
            }

            // Append any remaining text after the last reference.
            if (lastCopied < endIndex)
            {
                _builder.Append(input, lastCopied, endIndex - lastCopied);
            }
        }
    }
}
