// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Immutable, eagerly formatted warning payload. Context, project file, timestamp,
/// and thread identity belong to the current invocation, not to the cached result.
/// </summary>
internal sealed record TaskCacheWarning(
    string? Subcategory, string? Code, string? File, string? Message,
    string? HelpKeyword, string? SenderName, string? HelpLink,
    int LineNumber, int ColumnNumber, int EndLineNumber, int EndColumnNumber)
{
    internal const int MaximumCount = 1024;
    internal const int MaximumStringBytes = 1024 * 1024;
    internal const int MaximumPayloadBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding s_encoding = new(false, true);

    internal static TaskCacheWarning? Snapshot(BuildWarningEventArgs warning, out int size)
    {
        size = 0;
        // Unknown subclasses may carry additional mutable or structured state.
        if (warning.GetType() != typeof(BuildWarningEventArgs)
            || warning.LineNumber < 0 || warning.ColumnNumber < 0
            || warning.EndLineNumber < 0 || warning.EndColumnNumber < 0)
        {
            return null;
        }

        TaskCacheWarning value = new(warning.Subcategory, warning.Code, warning.File,
            warning.Message, warning.HelpKeyword, warning.SenderName, warning.HelpLink,
            warning.LineNumber, warning.ColumnNumber, warning.EndLineNumber, warning.EndColumnNumber);
        size = 4 * sizeof(int) + StringSize(value.Subcategory) + StringSize(value.Code)
            + StringSize(value.File) + StringSize(value.Message) + StringSize(value.HelpKeyword)
            + StringSize(value.SenderName) + StringSize(value.HelpLink);
        return value;
    }

    internal BuildWarningEventArgs ToEvent(BuildEventContext context) =>
        new(Subcategory, Code, File, LineNumber, ColumnNumber, EndLineNumber, EndColumnNumber,
            Message, HelpKeyword, SenderName, HelpLink, DateTime.UtcNow, messageArgs: null)
        {
            BuildEventContext = context,
        };

    internal static void Write(BinaryWriter writer, IReadOnlyList<TaskCacheWarning> warnings)
    {
        if (warnings.Count > MaximumCount)
        {
            throw new InvalidDataException("Too many cached task warnings.");
        }
        long start = writer.BaseStream.Position;
        writer.Write(warnings.Count);
        foreach (TaskCacheWarning warning in warnings)
        {
            WriteString(writer, warning.Subcategory);
            WriteString(writer, warning.Code);
            WriteString(writer, warning.File);
            WriteString(writer, warning.Message);
            WriteString(writer, warning.HelpKeyword);
            WriteString(writer, warning.SenderName);
            WriteString(writer, warning.HelpLink);
            writer.Write(warning.LineNumber);
            writer.Write(warning.ColumnNumber);
            writer.Write(warning.EndLineNumber);
            writer.Write(warning.EndColumnNumber);
            if (writer.BaseStream.Position - start > MaximumPayloadBytes)
            {
                throw new InvalidDataException("Cached task warnings exceed the supported size.");
            }
        }
    }

    internal static TaskCacheWarning[] Read(BinaryReader reader)
    {
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (remaining < sizeof(int) || remaining > MaximumPayloadBytes)
        {
            throw new InvalidDataException("Invalid cached task warning payload size.");
        }
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumCount)
        {
            throw new InvalidDataException("Invalid cached task warning count.");
        }
        TaskCacheWarning[] warnings = new TaskCacheWarning[count];
        for (int i = 0; i < count; i++)
        {
            TaskCacheWarning warning = new(ReadString(reader), ReadString(reader), ReadString(reader),
                ReadString(reader), ReadString(reader), ReadString(reader), ReadString(reader),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (warning.LineNumber < 0 || warning.ColumnNumber < 0
                || warning.EndLineNumber < 0 || warning.EndColumnNumber < 0)
            {
                throw new InvalidDataException("Invalid cached task warning location.");
            }
            warnings[i] = warning;
        }
        return warnings;
    }

    private static int StringSize(string? value)
    {
        if (value is null)
        {
            return sizeof(int);
        }
        if (value.Length > MaximumStringBytes)
        {
            throw new InvalidDataException("Cached task warning string exceeds the supported size.");
        }
        int bytes = s_encoding.GetByteCount(value);
        if (bytes > MaximumStringBytes)
        {
            throw new InvalidDataException("Cached task warning string exceeds the supported size.");
        }
        return sizeof(int) + bytes;
    }

    private static void WriteString(BinaryWriter writer, string? value)
    {
        StringSize(value);
        if (value is null)
        {
            writer.Write(-1);
            return;
        }
        byte[] bytes = s_encoding.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string? ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length == -1)
        {
            return null;
        }
        if (length < 0 || length > MaximumStringBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Invalid cached task warning string size.");
        }
        try
        {
            return s_encoding.GetString(reader.ReadBytes(length));
        }
        catch (DecoderFallbackException e)
        {
            throw new InvalidDataException("Invalid cached task warning text.", e);
        }
    }
}
