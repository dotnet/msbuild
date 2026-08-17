// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// Generic custom error events including extended data for event enriching.
/// Extended data are implemented by <see cref="IExtendedBuildEventArgs"/>
/// </summary>
public sealed class ExtendedBuildErrorEventArgs : BuildErrorEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    public string ExtendedType { get; set; }

    /// <inheritdoc />
    public Dictionary<string, string?>? ExtendedMetadata { get; set; }

    /// <inheritdoc />
    public string? ExtendedData { get; set; }

    /// <summary>
    /// Default constructor. Used for deserialization.
    /// </summary>
    internal ExtendedBuildErrorEventArgs() : this("undefined") { }

    /// <summary>
    /// This constructor specifies only type of extended data.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    public ExtendedBuildErrorEventArgs(string type) => ExtendedType = type;

    /// <summary>
    /// This constructor allows all event data to be initialized
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event sub-category</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    public ExtendedBuildErrorEventArgs(string type, string? subcategory, string? code, string? file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber,
        string? message, string? helpKeyword, string? senderName)
        : base(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName) => ExtendedType = type;

    /// <summary>
    /// This constructor which allows a timestamp to be set
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event sub-category</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    /// <param name="eventTimestamp">Timestamp when event was created</param>
    public ExtendedBuildErrorEventArgs(string type, string? subcategory, string? code, string? file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber,
        string? message, string? helpKeyword, string? senderName, DateTime eventTimestamp)
        : base(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, eventTimestamp) => ExtendedType = type;

    /// <summary>
    /// This constructor which allows a timestamp to be set
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event sub-category</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of event sender</param>
    /// <param name="eventTimestamp">Timestamp when event was created</param>
    /// <param name="messageArgs">message arguments</param>
    public ExtendedBuildErrorEventArgs(string type, string? subcategory, string? code, string? file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber,
        string? message, string? helpKeyword, string? senderName, DateTime eventTimestamp, params object[]? messageArgs)
        : base(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, eventTimestamp, messageArgs) => ExtendedType = type;

    /// <summary>
    /// This constructor which allows a timestamp to be set
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="subcategory">event sub-category</param>
    /// <param name="code">event code</param>
    /// <param name="file">file associated with the event</param>
    /// <param name="lineNumber">line number (0 if not applicable)</param>
    /// <param name="columnNumber">column number (0 if not applicable)</param>
    /// <param name="endLineNumber">end line number (0 if not applicable)</param>
    /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="helpLink">A link pointing to more information about the error </param>
    /// <param name="senderName">name of event sender</param>
    /// <param name="eventTimestamp">Timestamp when event was created</param>
    /// <param name="messageArgs">message arguments</param>
    public ExtendedBuildErrorEventArgs(string type, string? subcategory, string? code, string? file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber,
        string? message, string? helpKeyword, string? senderName, string? helpLink, DateTime eventTimestamp, params object[]? messageArgs)
        : base(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, helpLink, eventTimestamp, messageArgs) => ExtendedType = type;

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
