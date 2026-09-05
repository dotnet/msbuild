// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging;

/// <summary>
/// Metadata available before a length-framed binary log event is fully deserialized.
/// </summary>
public readonly struct BinaryLogEventMetadata
{
    internal BinaryLogEventMetadata(
        BinaryLogRecordKind recordKind,
        BuildEventContext? buildEventContext,
        BuildEventContext? originalBuildEventContext = null)
    {
        RecordKind = recordKind;
        BuildEventContext = buildEventContext;
        OriginalBuildEventContext = originalBuildEventContext;
    }

    /// <summary>
    /// Gets the serialized event type.
    /// </summary>
    public BinaryLogRecordKind RecordKind { get; }

    /// <summary>
    /// Gets the event's build context, or <see langword="null"/> when the event has no context.
    /// </summary>
    public BuildEventContext? BuildEventContext { get; }

    /// <summary>
    /// Gets the original context carried by a target-skipped event, or <see langword="null"/>.
    /// </summary>
    public BuildEventContext? OriginalBuildEventContext { get; }
}

/// <summary>
/// Decides whether a binary log event should be deserialized and dispatched.
/// </summary>
/// <param name="metadata">The metadata of the event about to be read.</param>
/// <returns><see langword="true"/> to keep the event; <see langword="false"/> to skip it.</returns>
/// <remarks>
/// Returning <see langword="false"/> skips the event. For length-framed binlogs the type-specific
/// payload is skipped without being deserialized. Auxiliary string, name/value-list and
/// embedded-content records are still read so retained events can be decoded correctly.
///
/// The filter is responsible for retaining a structurally consistent set of events. For example,
/// retaining a finish event while dropping its corresponding start event can produce a log that
/// downstream consumers cannot interpret correctly.
/// </remarks>
public delegate bool BinaryLogEventFilter(BinaryLogEventMetadata metadata);

/// <summary>
/// Wraps an exception thrown by a <see cref="BinaryLogEventFilter"/> callback.
/// </summary>
/// <remarks>
/// The wrapper distinguishes caller bugs in the filter from errors encountered while reading the
/// log, so filter failures abort the replay instead of being reported as recoverable read errors.
/// The exception thrown by the filter is available as <see cref="Exception.InnerException"/>.
/// </remarks>
public sealed class BinaryLogEventFilterException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryLogEventFilterException"/> class.
    /// </summary>
    /// <param name="innerException">The exception thrown by the filter callback.</param>
    public BinaryLogEventFilterException(Exception innerException)
        : base(ResourceUtilities.GetResourceString("Binlog_EventFilterThrew"), innerException)
    {
    }
}
