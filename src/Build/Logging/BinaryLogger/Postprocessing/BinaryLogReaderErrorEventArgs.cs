// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Materializes the error message.
    /// Until it's called the error message is not materialized and no string allocations are made.
    /// </summary>
    /// <returns>The error message.</returns>
    internal delegate string FormatErrorMessage();

    /// <summary>
    /// An event args for <see cref="IBuildEventArgsReaderNotifications.RecoverableReadError"/> event.
    /// </summary>
    public sealed class BinaryLogReaderErrorEventArgs : EventArgs
    {
        private readonly FormatErrorMessage _formatErrorMessage;

        internal BinaryLogReaderErrorEventArgs(
            ReaderErrorType errorType,
            BinaryLogRecordKind recordKind,
            FormatErrorMessage formatErrorMessage)
        {
            ErrorType = errorType;
            RecordKind = recordKind;
            _formatErrorMessage = formatErrorMessage;
        }

        /// <summary>
        /// Type of the error that occurred during reading.
        /// </summary>
        public ReaderErrorType ErrorType { get; }

        /// <summary>
        /// Kind of the record that encountered the error.
        /// </summary>
        public BinaryLogRecordKind RecordKind { get; }

        /// <summary>
        /// Materializes the error message.
        /// Until it's called the error message is not materialized and no string allocations are made.
        /// </summary>
        /// <returns>The error message.</returns>
        public string GetFormattedMessage() => _formatErrorMessage();
    }
}
