// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging;

/// <summary>
/// Metadata passed to a binary log event filter.
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
    /// Gets the event's build context, or <see langword="null"/> if absent.
    /// </summary>
    public BuildEventContext? BuildEventContext { get; }

    /// <summary>
    /// Gets the original context of a target-skipped event, or <see langword="null"/>.
    /// </summary>
    public BuildEventContext? OriginalBuildEventContext { get; }
}

/// <summary>
/// Decides whether a binary log event should be deserialized and dispatched.
/// </summary>
/// <param name="metadata">The event metadata.</param>
/// <returns><see langword="true"/> to keep the event; <see langword="false"/> to skip it.</returns>
/// <remarks>
/// Rejected payloads are skipped in length-framed logs (v18+). Legacy logs and
/// <see cref="TargetSkippedEventArgs"/> require deserialization; auxiliary records are always read.
/// Filters must preserve any event structure required by consumers, including matching start/finish events.
/// </remarks>
public delegate bool BinaryLogEventFilter(BinaryLogEventMetadata metadata);

/// <summary>
/// Wraps an exception thrown by a <see cref="BinaryLogEventFilter"/> callback.
/// </summary>
/// <remarks>
/// Filter failures abort replay; they are not recoverable read errors.
/// The original failure is preserved in <see cref="Exception.InnerException"/>.
/// On .NET Framework, legacy serialization preserves the exception and supplied record diagnostics
/// when marshaling exceptions across AppDomain boundaries.
/// </remarks>
#if NETFRAMEWORK
[Serializable]
#endif
public sealed class BinaryLogEventFilterException : Exception
{
    /// <summary>
    /// Creates a filter failure without record diagnostics.
    /// </summary>
    /// <param name="innerException">The exception thrown by the filter callback.</param>
    public BinaryLogEventFilterException(Exception innerException)
        : base(ResourceUtilities.GetResourceString("Binlog_EventFilterThrew"), innerException)
    {
    }

    /// <summary>
    /// Creates a filter failure with record diagnostics.
    /// </summary>
    /// <param name="metadata">The failing callback's event metadata.</param>
    /// <param name="recordNumber">The zero-based record number, including auxiliary records.</param>
    /// <param name="fileFormatVersion">The source binlog's format version.</param>
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
    /// Gets the record kind, or <see langword="null"/> if unavailable.
    /// </summary>
    public BinaryLogRecordKind? RecordKind { get; }

    /// <summary>
    /// Gets the build context passed to the filter, or <see langword="null"/> if unavailable.
    /// </summary>
    public BuildEventContext? BuildEventContext { get; }

    /// <summary>
    /// Gets the original target-skipped context, or <see langword="null"/> if unavailable.
    /// </summary>
    public BuildEventContext? OriginalBuildEventContext { get; }

    /// <summary>
    /// Gets the zero-based record number, including auxiliary and rejected records,
    /// or <see langword="null"/> if unavailable.
    /// </summary>
    public long? RecordNumber { get; }

    /// <summary>
    /// Gets the source binlog's format version, or <see langword="null"/> if unavailable.
    /// </summary>
    public int? FileFormatVersion { get; }

#if NETFRAMEWORK
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
    /// Serializes the exception and record diagnostics.
    /// </summary>
    /// <param name="info">The serialization data to populate.</param>
    /// <param name="context">The serialization context.</param>
#if FEATURE_SECURITY_PERMISSIONS
    [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter = true)]
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
#endif
}
