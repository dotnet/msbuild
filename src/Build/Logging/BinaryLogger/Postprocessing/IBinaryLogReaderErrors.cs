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
        /// </summary>
        event Action<BinaryLogReaderErrorEventArgs>? OnRecoverableReadError;
    }
}
