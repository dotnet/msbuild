// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// Generic custom event.
/// Extended data are implemented by <see cref="IExtendedBuildEventArgs"/>
/// </summary>
public sealed class ExtendedCustomBuildEventArgs : CustomBuildEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    public string ExtendedType { get; set; }

    /// <inheritdoc />
    public Dictionary<string, string?>? ExtendedMetadata { get; set; }

    /// <inheritdoc />
    public string? ExtendedData { get; set; }

    /// <summary>
    /// This constructor allows event data to be initialized.
    /// </summary>
    /// <seealso cref="IExtendedBuildEventArgs.ExtendedType"/>
    internal ExtendedCustomBuildEventArgs() : this("undefined") {}

    /// <summary>
    /// This constructor allows event data to be initialized.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <seealso cref="IExtendedBuildEventArgs.ExtendedType"/>
    public ExtendedCustomBuildEventArgs(string type) => ExtendedType = type;

    /// <summary>
    /// This constructor allows event data to be initialized.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of sender</param>
    public ExtendedCustomBuildEventArgs(string type, string? message, string? helpKeyword, string? senderName) : base(message, helpKeyword, senderName) => ExtendedType = type;

    /// <summary>
    /// This constructor allows event data to be initialized including timestamp.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of sender</param>
    /// <param name="eventTimestamp">Timestamp when event was created</param>
    public ExtendedCustomBuildEventArgs(string type, string? message, string? helpKeyword, string? senderName, DateTime eventTimestamp) : base(message, helpKeyword, senderName, eventTimestamp) => ExtendedType = type;

    /// <summary>
    /// This constructor allows event data to be initialized including timestamp.
    /// </summary>
    /// <param name="type">Type of <see cref="IExtendedBuildEventArgs.ExtendedType"/>.</param>
    /// <param name="message">text message</param>
    /// <param name="helpKeyword">help keyword </param>
    /// <param name="senderName">name of sender</param>
    /// <param name="eventTimestamp">Timestamp when event was created</param>
    /// <param name="messageArgs">Message arguments</param>
    public ExtendedCustomBuildEventArgs(string type, string? message, string? helpKeyword, string? senderName, DateTime eventTimestamp, params object[]? messageArgs) : base(message, helpKeyword, senderName, eventTimestamp, messageArgs) => ExtendedType = type;

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
