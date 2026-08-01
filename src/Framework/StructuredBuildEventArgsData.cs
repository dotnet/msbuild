// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Build.Framework;

/// <summary>
/// Maps structured state onto the existing extended-event transport.
/// </summary>
/// <remarks>
/// Reusing extended metadata avoids a new node protocol and lets older readers retain the visible
/// diagnostic. Numeric key prefixes preserve occurrence order independently of dictionary
/// enumeration, while a value tag distinguishes null from an empty string across binary-log
/// readers whose legacy metadata API cannot otherwise preserve that distinction.
/// </remarks>
internal static class StructuredBuildEventArgsData
{
    internal const string EventType = "MSBuild.StructuredLogging";

    private const char Separator = ':';
    private const char NullValue = '0';
    private const char StringValue = '1';
    private const int IndexWidth = 8;
    private const int MaximumValueCount = 99_999_999;

    internal static void Set(
        IExtendedBuildEventArgs buildEvent,
        string originalFormat,
        IReadOnlyList<KeyValuePair<string, string?>> values)
    {
        ArgumentNullException.ThrowIfNull(buildEvent);
        ArgumentNullException.ThrowIfNull(originalFormat);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > MaximumValueCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                values.Count,
                $"Structured messages support at most {MaximumValueCount:N0} values.");
        }

        var metadata = new Dictionary<string, string?>(values.Count, StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            KeyValuePair<string, string?> value = values[i];
            string key = i.ToString($"D{IndexWidth}", CultureInfo.InvariantCulture) + Separator + value.Key;
            metadata.Add(key, value.Value is null ? NullValue.ToString() : StringValue + value.Value);
        }

        buildEvent.ExtendedType = EventType;
        buildEvent.ExtendedData = originalFormat;
        buildEvent.ExtendedMetadata = metadata;
        Apply(buildEvent);
    }

    internal static void Apply(IExtendedBuildEventArgs buildEvent)
    {
        if (!string.Equals(buildEvent.ExtendedType, EventType, StringComparison.Ordinal)
            || buildEvent.ExtendedData is null
            || buildEvent.ExtendedMetadata is null)
        {
            return;
        }

        var result = new KeyValuePair<string, string?>[buildEvent.ExtendedMetadata.Count];
        var seen = new bool[result.Length];
        foreach (KeyValuePair<string, string?> entry in buildEvent.ExtendedMetadata)
        {
            if (!TryDecode(entry, result, seen))
            {
                return;
            }
        }

        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i])
            {
                return;
            }
        }

        switch (buildEvent)
        {
            case ExtendedBuildMessageEventArgs message:
                message.OriginalFormat = buildEvent.ExtendedData;
                message.StructuredValues = result;
                break;
            case ExtendedBuildWarningEventArgs warning:
                warning.OriginalFormat = buildEvent.ExtendedData;
                warning.StructuredValues = result;
                break;
            case ExtendedBuildErrorEventArgs error:
                error.OriginalFormat = buildEvent.ExtendedData;
                error.StructuredValues = result;
                break;
        }
    }

    private static bool TryDecode(
        KeyValuePair<string, string?> entry,
        KeyValuePair<string, string?>[] destination,
        bool[] seen)
    {
        if (entry.Key.Length <= IndexWidth
            || entry.Key[IndexWidth] != Separator
            || !int.TryParse(entry.Key.Substring(0, IndexWidth), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || (uint)index >= (uint)destination.Length
            || seen[index]
            || entry.Value is not { Length: > 0 } encoded)
        {
            return false;
        }

        string? value;
        if (encoded[0] == NullValue && encoded.Length == 1)
        {
            value = null;
        }
        else if (encoded[0] == StringValue)
        {
            value = encoded.Substring(1);
        }
        else
        {
            return false;
        }

        destination[index] = new KeyValuePair<string, string?>(entry.Key.Substring(IndexWidth + 1), value);
        seen[index] = true;
        return true;
    }
}
