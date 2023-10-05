// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    internal interface IEmbeddedContentSource
    {
        
        /// <summary>
        /// Raised when the log reader encounters a project import archive (embedded content) in the stream.
        /// The subscriber must read the exactly given length of binary data from the stream - otherwise exception is raised.
        /// If no subscriber is attached, the data is skipped.
        /// </summary>
        event Action<EmbeddedContentEventArgs> EmbeddedContentRead;
    }
}
