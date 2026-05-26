// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Collections.Generic;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Implemented by loggers that write the build log to one or more files on disk
    /// (for example <see cref="FileLogger"/> and <see cref="BinaryLogger"/>).
    /// </summary>
    /// <remarks>
    /// This is used solely to surface the log file paths in the build summary printed
    /// by the console logger at the end of a build so that users can easily locate the
    /// log files that were produced. It is not intended to represent project build
    /// outputs (e.g. produced assemblies) or any other artifacts unrelated to logging.
    /// </remarks>
    internal interface IFileOutputLogger
    {
        /// <summary>
        /// Gets the absolute paths of the log files that this logger writes to.
        /// Reported in the end-of-build summary emitted by the console logger.
        /// </summary>
        IReadOnlyList<string> OutputFilePaths { get; }
    }
}
