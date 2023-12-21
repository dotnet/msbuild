// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging;

/// <summary>
/// Type of the error that occurred during reading.
/// </summary>
public enum ReaderErrorType
{
    /// <summary>
    /// The encountered event is completely unknown to the reader. It cannot interpret any part of it.
    /// </summary>
    UnknownEventType,

    /// <summary>
    /// The encountered event is known to the reader and reader is able to read the event as it knows it.
    /// However there are some extra data (append only extension to the event in future version), that reader cannot interpret,
    ///  it can only skip it.
    /// </summary>
    UnknownEventData,

    /// <summary>
    /// The encountered event type is known to the reader, but the reader cannot interpret the data of the event.
    /// This is probably caused by an event definition changing more than just adding fields.
    /// The reader can only skip the event in full.
    /// </summary>
    UnknownFormatOfEventData,
}
