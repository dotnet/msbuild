// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Framework;

/// <summary>
/// Exposes the invariant template and ordered values associated with a structured build event.
/// </summary>
/// <remarks>
/// The structured state is intentionally independent from <see cref="BuildEventArgs.Message"/>.
/// Loggers often aggregate or filter by template without needing the complete display string, and
/// <see cref="BuildEventArgs.Message"/> may be materialized lazily. Keeping this contract separate
/// also prevents lazy message materialization from discarding machine-readable names and values.
/// </remarks>
public interface IStructuredBuildEventArgs
{
    /// <summary>
    /// Gets the invariant message template whose named holes correspond positionally to
    /// <see cref="StructuredValues"/>.
    /// </summary>
    /// <remarks>
    /// This is the structured-logging equivalent of Microsoft.Extensions.Logging's
    /// <c>{OriginalFormat}</c> convention. It is nullable so consumers can safely probe event types
    /// that expose the contract before deserialization has populated their structured state.
    /// </remarks>
    string? OriginalFormat { get; }

    /// <summary>
    /// Gets the ordered, uniquely named values captured for the message template.
    /// Values are formatted once using the task's current culture before they cross a process boundary.
    /// </summary>
    /// <remarks>
    /// An ordered list preserves the relationship between template occurrences and values without
    /// depending on dictionary enumeration behavior. Names are unique so consumers may safely build
    /// a lookup when order is not relevant. Capturing the culture-sensitive text once ensures in-process
    /// logging and replay render identically. Null values remain null rather than being conflated with an
    /// empty string.
    /// </remarks>
    IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues { get; }
}

[Serializable]
internal struct StructuredBuildEventState
{
    internal const int MaximumValueCount = ushort.MaxValue;

    private string? _formattedMessage;
    private string? _originalFormatOverride;

    internal IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues { get; private set; }

    internal string? GetOriginalFormat(string? rawMessage) =>
        StructuredValues is null ? null : _originalFormatOverride ?? rawMessage;

    internal void Set(
        string? rawMessage,
        string originalFormat,
        IReadOnlyList<KeyValuePair<string, string?>> values)
    {
        ArgumentNullException.ThrowIfNull(originalFormat);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Count, MaximumValueCount);

        _originalFormatOverride = string.Equals(rawMessage, originalFormat, StringComparison.Ordinal)
            ? null
            : originalFormat;
        StructuredValues = values;
        _formattedMessage = null;
    }

    internal string? GetFormattedMessage(string? template)
    {
        if (template is null || StructuredValues is null || _originalFormatOverride is not null)
        {
            return template;
        }

        string? formatted = Volatile.Read(ref _formattedMessage);
        if (formatted is not null)
        {
            return formatted;
        }

        formatted = Format(template, StructuredValues);
        Interlocked.CompareExchange(ref _formattedMessage, formatted, null);
        return _formattedMessage;
    }

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.WriteOptionalString(_originalFormatOverride);
        int count = StructuredValues?.Count ?? 0;
        writer.Write7BitEncodedInt(count);
        for (int i = 0; i < count; i++)
        {
            KeyValuePair<string, string?> value = StructuredValues![i];
            writer.Write(value.Key);
            writer.WriteOptionalString(value.Value);
        }
    }

    internal void CreateFromStream(BinaryReader reader)
    {
        _originalFormatOverride = reader.ReadOptionalString();
        int count = reader.Read7BitEncodedInt();
        if ((uint)count > MaximumValueCount)
        {
            throw new InvalidDataException($"Structured event value count {count} exceeds {MaximumValueCount}.");
        }

        var values = new KeyValuePair<string, string?>[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = new KeyValuePair<string, string?>(
                reader.ReadString(),
                reader.ReadOptionalString());
        }

        StructuredValues = values;
        _formattedMessage = null;
    }

    private static string Format(
        string template,
        IReadOnlyList<KeyValuePair<string, string?>> values)
    {
        var builder = new ValueStringBuilder(template.Length);
        int valueIndex = 0;
        for (int i = 0; i < template.Length;)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    builder.Append('{');
                    i += 2;
                    continue;
                }

                int close = template.IndexOf('}', i + 1);
                if (close < 0 || valueIndex >= values.Count)
                {
                    builder.Dispose();
                    return template;
                }

                ReadOnlySpan<char> hole = template.AsSpan(i + 1, close - i - 1);
                int comma = hole.IndexOf(',');
                int colon = hole.IndexOf(':');
                int alignmentEnd = colon >= 0 ? colon : hole.Length;
                int alignment = 0;
                if (comma >= 0
                    && !TryParseAlignment(
                        hole.Slice(comma + 1, alignmentEnd - comma - 1),
                        out alignment))
                {
                    builder.Dispose();
                    return template;
                }

                AppendAligned(ref builder, values[valueIndex++].Value ?? string.Empty, alignment);
                i = close + 1;
                continue;
            }

            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                builder.Append('}');
                i += 2;
                continue;
            }

            builder.Append(c);
            i++;
        }

        if (valueIndex == values.Count)
        {
            return builder.ToStringAndDispose();
        }

        builder.Dispose();
        return template;
    }

    private static void AppendAligned(ref ValueStringBuilder builder, string value, int alignment)
    {
        if (alignment == 0 || value.Length >= Math.Abs(alignment))
        {
            builder.Append(value);
        }
        else if (alignment > 0)
        {
            builder.Append(' ', alignment - value.Length);
            builder.Append(value);
        }
        else
        {
            builder.Append(value);
            builder.Append(' ', -alignment - value.Length);
        }
    }

    private static bool TryParseAlignment(ReadOnlySpan<char> value, out int alignment)
    {
        while (!value.IsEmpty && char.IsWhiteSpace(value[0]))
        {
            value = value.Slice(1);
        }

        while (!value.IsEmpty && char.IsWhiteSpace(value[value.Length - 1]))
        {
            value = value.Slice(0, value.Length - 1);
        }

        bool negative = !value.IsEmpty && value[0] == '-';
        if (negative || (!value.IsEmpty && value[0] == '+'))
        {
            value = value.Slice(1);
        }

        if (value.IsEmpty)
        {
            alignment = 0;
            return false;
        }

        int result = 0;
        foreach (char c in value)
        {
            if (c is < '0' or > '9')
            {
                alignment = 0;
                return false;
            }

            try
            {
                result = checked((result * 10) + (c - '0'));
            }
            catch (OverflowException)
            {
                alignment = 0;
                return false;
            }
        }

        alignment = negative ? -result : result;
        return true;
    }
}
