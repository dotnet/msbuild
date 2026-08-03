// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework;

/// <summary>
/// Keeps structured message state separate from the general-purpose extended-event contract.
/// </summary>
/// <remarks>
/// These types are internal because loggers consume the stable <see cref="IStructuredBuildEventArgs"/>
/// contract. Dedicated types let node and binary-log transports serialize ordered values directly
/// without forcing every extended event to carry structured state or dictionary framing.
/// </remarks>
[Serializable]
internal sealed class StructuredBuildMessageEventArgs : BuildMessageEventArgs, IStructuredBuildEventArgs
{
    private StructuredBuildEventState _structuredState;

    internal StructuredBuildMessageEventArgs()
    {
    }

    internal StructuredBuildMessageEventArgs(
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string message,
        string originalFormat,
        IReadOnlyList<KeyValuePair<string, string?>> values,
        string? helpKeyword,
        string? senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(
            subcategory,
            code,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            message,
            helpKeyword,
            senderName,
            importance,
            eventTimestamp)
    {
        _structuredState.Set(message, originalFormat, values);
    }

    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        _structuredState.CreateFromStream(reader);
    }
}

[Serializable]
internal sealed class StructuredBuildWarningEventArgs : BuildWarningEventArgs, IStructuredBuildEventArgs
{
    private StructuredBuildEventState _structuredState;

    internal StructuredBuildWarningEventArgs()
    {
    }

    internal StructuredBuildWarningEventArgs(
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string message,
        string originalFormat,
        IReadOnlyList<KeyValuePair<string, string?>> values,
        string? helpKeyword,
        string? senderName,
        string? helpLink,
        DateTime eventTimestamp)
        : base(
            subcategory,
            code,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            message,
            helpKeyword,
            senderName,
            helpLink,
            eventTimestamp)
    {
        _structuredState.Set(message, originalFormat, values);
    }

    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        _structuredState.CreateFromStream(reader);
    }
}

[Serializable]
internal sealed class StructuredBuildErrorEventArgs : BuildErrorEventArgs, IStructuredBuildEventArgs
{
    private StructuredBuildEventState _structuredState;

    internal StructuredBuildErrorEventArgs()
    {
    }

    internal StructuredBuildErrorEventArgs(
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string message,
        string originalFormat,
        IReadOnlyList<KeyValuePair<string, string?>> values,
        string? helpKeyword,
        string? senderName,
        string? helpLink,
        DateTime eventTimestamp)
        : base(
            subcategory,
            code,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            message,
            helpKeyword,
            senderName,
            helpLink,
            eventTimestamp)
    {
        _structuredState.Set(message, originalFormat, values);
    }

    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        _structuredState.CreateFromStream(reader);
    }
}
