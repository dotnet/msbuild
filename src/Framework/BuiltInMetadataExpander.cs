// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Framework;

/// <summary>
///  Substitutes built-in metadata references, such as <c>%(Filename)</c>, with the values they denote for a
///  given item.
/// </summary>
/// <remarks>
///  <para>
///   Item definition metadata may reference built-in metadata. Such values are stored unexpanded so that they
///   follow the item they are read from, and are substituted whenever the metadata is read.
///  </para>
///  <para>
///   This is a deliberately minimal substitute for the evaluation expander, which also handles properties, item
///   vectors, custom metadata, transforms and truncation. Built-in metadata references are the only expression
///   form that survives evaluation unexpanded. It lives here because the evaluation expander is internal to
///   <c>Microsoft.Build</c>, while this is needed by <c>MSBuild</c> and <c>Microsoft.Build.Tasks</c> as well.
///  </para>
/// </remarks>
internal static class BuiltInMetadataExpander
{
    /// <summary>
    ///  Expands every built-in metadata reference in <paramref name="escapedValue"/> against the given item.
    ///  Anything else, including a reference that cannot be satisfied, is left untouched.
    /// </summary>
    /// <param name="escapedValue">The escaped value to expand.</param>
    /// <param name="escapedItemSpec">The escaped item spec that built-in metadata is derived from.</param>
    /// <param name="escapedDefiningProject">The escaped path of the project that defined the item.</param>
    /// <param name="escapedRecursiveDir">
    ///  The item's RecursiveDir, which comes from the wildcard the item was expanded from rather than the item spec.
    /// </param>
    /// <param name="cache">Cache of already derived modifier values for this item.</param>
    /// <returns>The value with built-in metadata references substituted.</returns>
    internal static string? Expand(
        string? escapedValue,
        string escapedItemSpec,
        string? escapedDefiningProject,
        string? escapedRecursiveDir,
        ref ItemSpecModifiers.Cache cache)
    {
        int index = escapedValue is null ? -1 : escapedValue.IndexOf("%(", StringComparison.Ordinal);

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
                }

                index = escapedValue.IndexOf("%(", closingParenthesis + 1, StringComparison.Ordinal);
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
    ///  Reads the metadata name between <c>%(</c> and its closing parenthesis and resolves it to a built-in
    ///  metadata kind, tolerating surrounding whitespace as the evaluation expander does. A name qualified by an
    ///  item type is rejected, since the engine resolves built-in metadata against an untyped table and so never
    ///  satisfies one either.
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
