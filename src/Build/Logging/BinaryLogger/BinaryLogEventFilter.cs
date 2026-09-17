// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
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
/// payload is skipped without being deserialized, except for <see cref="TargetSkippedEventArgs"/>,
/// whose original build context is part of that payload. Auxiliary string, name/value-list and
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
/// Exceptions raised by the reader also identify the record, its build contexts, and the log's
/// file format version. These details and the original exception are preserved during serialization.
/// </remarks>
[Serializable]
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

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryLogEventFilterException"/> class
    /// with information about the record being filtered.
    /// </summary>
    /// <param name="metadata">The metadata passed to the failing filter callback.</param>
    /// <param name="recordNumber">The zero-based record number, including auxiliary records.</param>
    /// <param name="fileFormatVersion">The file format version of the binary log being read.</param>
    /// <param name="innerException">The exception thrown by the filter callback.</param>
    public BinaryLogEventFilterException(
        BinaryLogEventMetadata metadata,
        long recordNumber,
        int fileFormatVersion,
        Exception innerException)
        : base(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            "Binlog_EventFilterThrewWithContext",
            recordNumber,
            metadata.RecordKind,
            fileFormatVersion,
            metadata.BuildEventContext,
            metadata.OriginalBuildEventContext), innerException)
    {
        RecordKind = metadata.RecordKind;
        BuildEventContext = metadata.BuildEventContext;
        OriginalBuildEventContext = metadata.OriginalBuildEventContext;
        RecordNumber = recordNumber;
        FileFormatVersion = fileFormatVersion;
    }

    /// <summary>
    /// Gets the kind of record being filtered, or <see langword="null"/> if no record information was supplied.
    /// </summary>
    public BinaryLogRecordKind? RecordKind { get; }

    /// <summary>
    /// Gets the build context passed to the filter, or <see langword="null"/> if unavailable.
    /// </summary>
    public BuildEventContext? BuildEventContext { get; }

    /// <summary>
    /// Gets the original context of a target-skipped event passed to the filter,
    /// or <see langword="null"/> if unavailable.
    /// </summary>
    public BuildEventContext? OriginalBuildEventContext { get; }

    /// <summary>
    /// Gets the zero-based record number, including auxiliary and rejected records,
    /// or <see langword="null"/> if no record information was supplied.
    /// </summary>
    public long? RecordNumber { get; }

    /// <summary>
    /// Gets the file format version of the binary log being read,
    /// or <see langword="null"/> if no record information was supplied.
    /// </summary>
    public int? FileFormatVersion { get; }

#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    private BinaryLogEventFilterException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        RecordKind = (BinaryLogRecordKind?)info.GetValue(nameof(RecordKind), typeof(BinaryLogRecordKind?));
        BuildEventContext = (BuildEventContext?)info.GetValue(nameof(BuildEventContext), typeof(BuildEventContext));
        OriginalBuildEventContext = (BuildEventContext?)info.GetValue(nameof(OriginalBuildEventContext), typeof(BuildEventContext));
        RecordNumber = (long?)info.GetValue(nameof(RecordNumber), typeof(long?));
        FileFormatVersion = (int?)info.GetValue(nameof(FileFormatVersion), typeof(int?));
    }

    /// <summary>
    /// Serializes the record information together with the base exception and its inner exception.
    /// </summary>
    /// <param name="info">The serialization data to populate.</param>
    /// <param name="context">The serialization context.</param>
#if FEATURE_SECURITY_PERMISSIONS
    [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter = true)]
#endif
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(RecordKind), RecordKind, typeof(BinaryLogRecordKind?));
        info.AddValue(nameof(BuildEventContext), BuildEventContext, typeof(BuildEventContext));
        info.AddValue(nameof(OriginalBuildEventContext), OriginalBuildEventContext, typeof(BuildEventContext));
        info.AddValue(nameof(RecordNumber), RecordNumber, typeof(long?));
        info.AddValue(nameof(FileFormatVersion), FileFormatVersion, typeof(int?));
    }
}
