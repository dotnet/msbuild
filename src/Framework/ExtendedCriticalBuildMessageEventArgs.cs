// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// Critical message events arguments including extended data for event enriching.
/// Extended data are implemented by <see cref="IExtendedBuildEventArgs"/>
/// </summary>
public sealed class ExtendedCriticalBuildMessageEventArgs : CriticalBuildMessageEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    public string ExtendedType { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string?>? ExtendedMetadata { get; set; }

    /// <inheritdoc />
    public string? ExtendedData { get; set; }

    /// <summary>
    /// This constructor allows all event data to be initialized
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event subcategory</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    public ExtendedCriticalBuildMessageEventArgs(
        string type,
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string? message,
        string? helpKeyword,
        string? senderName)
        : this(type, subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, DateTime.UtcNow)
    {
        // do nothing
    }

    /// <summary>
    /// This constructor allows timestamp to be set
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event subcategory</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    /// <param name="eventTimestamp">custom timestamp for the event</param>
    public ExtendedCriticalBuildMessageEventArgs(
        string type,
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string? message,
        string? helpKeyword,
        string? senderName,
        DateTime eventTimestamp)
        : this(type, subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, eventTimestamp, null!)
    {
        // do nothing
    }

    /// <summary>
    /// This constructor allows timestamp to be set
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event subcategory</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    /// <param name="eventTimestamp">custom timestamp for the event</param>
    /// <param name="messageArgs">message arguments</param>
    public ExtendedCriticalBuildMessageEventArgs(
        string type,
        string? subcategory,
        string? code,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        string? message,
        string? helpKeyword,
        string? senderName,
        DateTime eventTimestamp,
        params object[]? messageArgs)
        //// Force importance to High.
        : base(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, eventTimestamp, messageArgs) => ExtendedType = type;

    /// <summary>
    /// Default constructor. Used for deserialization.
    /// </summary>
    internal ExtendedCriticalBuildMessageEventArgs() : this("undefined")
    {
        // do nothing
    }

    /// <summary>
    /// This constructor specifies only type of extended data.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    public ExtendedCriticalBuildMessageEventArgs(string type) => ExtendedType = type;

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        writer.WriteExtendedBuildEventData(this);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        reader.ReadExtendedBuildEventData(this);
    }
}
