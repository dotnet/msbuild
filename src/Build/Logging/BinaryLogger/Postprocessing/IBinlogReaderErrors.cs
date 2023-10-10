// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging
{
    public enum ReaderErrorType
    {
        UnsupportedFileFormat,
        UnkownEventType,
        UnknownEventData,
        UnknownFormatOfEventData,
    }

    public interface IBinlogReaderErrors
    {
        /// <summary>
        /// Receives recoverable errors during reading.
        /// </summary>
        event Action<ReaderErrorType, string>? OnRecoverableReadError;
    }
}
