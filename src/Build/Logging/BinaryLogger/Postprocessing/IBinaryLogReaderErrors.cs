// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    public interface IBinaryLogReaderErrors
    {
        /// <summary>
        /// Receives recoverable errors during reading.
        /// Communicates type of the error, kind of the record that encountered the error and the message detailing the error.
        /// In case of <see cref="ReaderErrorType.UnknownEventData"/> this is raised before returning the structured representation of a build event
        /// that has some extra unknown data in the binlog. In case of other error types this event is raised and the offending build event is skipped and not returned.
        /// </summary>
        event Action<BinaryLogReaderErrorEventArgs>? OnRecoverableReadError;
    }
}
