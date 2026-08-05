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
/// Loggers use the stable <see cref="IStructuredBuildEventArgs"/> contract instead of these internal types.
/// Dedicated types let node and binary-log transports serialize the ordered values directly.
/// Other extended events do not carry structured state or dictionary framing.
/// </remarks>
[Serializable]
internal sealed class StructuredBuildMessageEventArgs : BuildMessageEventArgs, IStructuredBuildEventArgs
{
    private StructuredBuildEventState _structuredState;

    /// <summary>
    /// Creates an empty event for deserialization.
    /// </summary>
    internal StructuredBuildMessageEventArgs()
    {
    }

    /// <summary>
    /// Creates a structured message event without creating its display text.
    /// </summary>
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

    /// <inheritdoc />
    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    /// <inheritdoc />
    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    /// <inheritdoc />
    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Creates an empty event for deserialization.
    /// </summary>
    internal StructuredBuildWarningEventArgs()
    {
    }

    /// <summary>
    /// Creates a structured warning event without creating its display text.
    /// </summary>
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

    /// <inheritdoc />
    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    /// <inheritdoc />
    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    /// <inheritdoc />
    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Creates an empty event for deserialization.
    /// </summary>
    internal StructuredBuildErrorEventArgs()
    {
    }

    /// <summary>
    /// Creates a structured error event without creating its display text.
    /// </summary>
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

    /// <inheritdoc />
    public string? OriginalFormat => _structuredState.GetOriginalFormat(RawMessage);

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues =>
        _structuredState.StructuredValues;

    /// <inheritdoc />
    public override string? Message => _structuredState.GetFormattedMessage(base.Message);

    /// <inheritdoc />
    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        _structuredState.WriteToStream(writer);
    }

    /// <inheritdoc />
    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        _structuredState.CreateFromStream(reader);
    }
}
