// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging;

/// <summary>
/// Type of the error that occurred during reading.
/// </summary>
public enum ReaderErrorType
{
    /// <summary>
    /// The encountered event is completely unknown to the reader. It cannot interpret neither a part of it.
    /// </summary>
    UnkownEventType,

    /// <summary>
    /// The encountered event is known to the reader and reader is able to read the event as it knows it.
    /// However there are some extra data (append only extension to the event in future version), that reader cannot interpret,
    ///  it can only skip it.
    /// </summary>
    UnknownEventData,

    /// <summary>
    /// The encountered event is known to the reader, however the reader cannot interpret the data of the event.
    /// This is probably caused by the fact that the event definition changed in the future revision in other than append-only manner.
    /// For this reason reader can only skip the event in full.
    /// </summary>
    UnknownFormatOfEventData,
}
