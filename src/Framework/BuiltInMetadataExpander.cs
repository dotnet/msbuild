// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.StringTools;

namespace Microsoft.Build.Framework;

/// <summary>
///  Expands built-in metadata references, such as <c>%(Filename)</c>, against an item.
/// </summary>
/// <remarks>
///  A deliberately minimal stand-in for the evaluation expander, which also handles properties, item vectors,
///  custom metadata, transforms and truncation. Built-in metadata references are the only expression form that
///  survives evaluation unexpanded, so they are the only one that can reach a task host still unexpanded. This
///  lives in Framework because the evaluation expander is internal to <c>Microsoft.Build</c>, while this is needed
///  by <c>MSBuild</c> and <c>Microsoft.Build.Tasks</c> too.
///
///  Keep in step with <c>Expander.ExpandIntoStringLeaveEscaped</c> under <c>ExpanderOptions.ExpandBuiltInMetadata</c>,
///  which is what <c>ProjectItemInstance.TaskItem.GetMetadataEscaped</c> uses for the same job in-proc.
/// </remarks>
internal static class BuiltInMetadataExpander
{
    /// <summary>
    ///  Expands every built-in metadata reference in <paramref name="escapedValue"/> against the given item.
    ///  Anything else, including a reference that cannot be satisfied, is left as it is.
    /// </summary>
    /// <param name="escapedValue">The escaped value to expand.</param>
    /// <param name="escapedItemSpec">The escaped item spec that built-in metadata is derived from.</param>
    /// <param name="escapedDefiningProject">The escaped path of the project that defined the item.</param>
    /// <param name="escapedRecursiveDir">
    ///  The item's RecursiveDir, which comes from the wildcard the item was expanded from rather than the item spec.
    /// </param>
    /// <param name="cache">Cache of already derived modifier values for this item.</param>
    /// <returns>The value with built-in metadata references expanded.</returns>
    internal static string? Expand(
        string? escapedValue,
        string escapedItemSpec,
        string? escapedDefiningProject,
        string? escapedRecursiveDir,
        ref ItemSpecModifiers.Cache cache)
    {
        int index = escapedValue is null ? -1 : IndexOfMetadataMarker(escapedValue, 0);

        if (index < 0)
        {
            return escapedValue;
        }

        SpanBasedStringBuilder? builder = null;
        int copiedUpTo = 0;

        try
        {
            while (index >= 0)
            {
                // No closing parenthesis anywhere after this point means no reference can close, so stop.
                int closingParenthesis = escapedValue!.IndexOf(')', index + 2);

                if (closingParenthesis < 0)
                {
                    break;
                }

                if (TryGetModifier(escapedValue, index + 2, closingParenthesis, out ItemSpecModifierKind kind))
                {
                    builder ??= Strings.GetSpanBasedStringBuilder();
                    builder.Append(escapedValue, copiedUpTo, index - copiedUpTo);
                    builder.Append(kind is ItemSpecModifierKind.RecursiveDir
                        ? escapedRecursiveDir ?? string.Empty
                        : ItemSpecModifiers.GetItemSpecModifier(escapedItemSpec, kind, currentDirectory: null, escapedDefiningProject, ref cache));
                    copiedUpTo = closingParenthesis + 1;

                    index = IndexOfMetadataMarker(escapedValue, copiedUpTo);
                }
                else
                {
                    // This "%(" does not start a reference. Resume just after it rather than after the
                    // parenthesis, because a well formed reference can begin inside the text it spanned,
                    // as in "%(foo%(Filename)". The evaluation expander advances the same way.
                    index = IndexOfMetadataMarker(escapedValue, index + 2);
                }
            }

            if (builder is null)
            {
                return escapedValue;
            }

            builder.Append(escapedValue!, copiedUpTo, escapedValue!.Length - copiedUpTo);
            return builder.ToString();
        }
        finally
        {
            builder?.Dispose();
        }
    }

    /// <summary>
    ///  Finds the first <c>%(</c> at or after <paramref name="startIndex"/>, or -1 if there is none.
    ///  Does not check that a well formed reference follows it.
    /// </summary>
    /// <remarks>
    ///  <c>IndexOf(char)</c> vectorizes, and is significantly faster than an ordinal two-character search when
    ///  the marker is absent, which is the usual case for a metadata value. So look for <c>%</c> alone and test
    ///  the next character separately.
    /// </remarks>
    internal static int IndexOfMetadataMarker(string value, int startIndex)
    {
        int markerIndex = value.IndexOf('%', startIndex);

        // A marker in the last position has no room for the parenthesis.
        while (markerIndex >= 0 && markerIndex < value.Length - 1)
        {
            int nextIndex = markerIndex + 1;

            if (value[nextIndex] == '(')
            {
                return markerIndex;
            }

            markerIndex = value.IndexOf('%', nextIndex);
        }

        return -1;
    }

    /// <summary>
    ///  Reads the metadata name between <c>%(</c> and its closing parenthesis and resolves it to a built-in
    ///  metadata kind, allowing surrounding whitespace as the evaluation expander does. A name qualified by an item
    ///  type is rejected, since the engine resolves built-in metadata against a table with no item type and so
    ///  never satisfies one either.
    /// </summary>
    private static bool TryGetModifier(string value, int start, int end, out ItemSpecModifierKind kind)
    {
        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        if (end <= start)
        {
            kind = default;
            return false;
        }

        return ItemSpecModifiers.TryGetModifierKind(value.Substring(start, end - start), out kind);
    }
}
