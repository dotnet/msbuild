// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// An interface for notifications from BuildEventArgsReader
    /// </summary>
    public interface IBuildEventArgsReaderNotifications
    {
        /// <summary>
        /// An event that allows the subscriber to be notified when a string is read from the binary log.
        /// Subscriber may adjust the string by setting <see cref="StringReadEventArgs.StringToBeUsed"/> property.
        /// The passed event arg can be reused and should not be stored.
        /// </summary>
        event Action<StringReadEventArgs>? StringReadDone;

        /// <summary>
        /// An event that allows the caller to be notified when an embedded file is encountered in the binary log.
        /// When subscriber is OK with greedy reading entire content of the file and is interested only in the individual strings (e.g. for sensitive data redaction purposes),
        ///  it can simplify subscribing to this event, by using handler with same signature as handler for <see cref="IBuildEventArgsReaderNotifications.StringReadDone"/> and wrapping it via
        /// <see cref="ArchiveFileEventArgsExtensions.ToArchiveFileHandler"/> extension.
        /// </summary>
        /// <example>
        /// <code>
        /// private void OnStringReadDone(StringReadEventArgs e)
        /// {
        ///     e.StringToBeUsed = e.StringToBeUsed.Replace("foo", "bar");
        /// }
        ///
        /// private void SubscribeToEvents()
        /// {
        ///     reader.StringReadDone += OnStringReadDone;
        ///     reader.ArchiveFileEncountered += ((Action&lt;StringReadEventArgs&gt;)OnStringReadDone).ToArchiveFileHandler();
        /// }
        /// </code>
        /// </example>
        event Action<ArchiveFileEventArgs>? ArchiveFileEncountered;

        /// <summary>
        /// Receives recoverable errors during reading.
        /// Communicates type of the error, kind of the record that encountered the error and the message detailing the error.
        /// In case of <see cref="ReaderErrorType.UnknownEventData"/> this is raised before returning the structured representation of a build event
        /// that has some extra unknown data in the binlog. In case of other error types this event is raised and the offending build event is skipped and not returned.
        /// </summary>
        event Action<BinaryLogReaderErrorEventArgs>? RecoverableReadError;
    }
}
