// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.FileAccess
{
    /// <summary>
    /// File access data.
    /// </summary>
    /// <param name="Operation">The operation that performed the file access.</param>
    /// <param name="RequestedAccess">The requested access.</param>
    /// <param name="ProcessId">The process id.</param>
    /// <param name="Error">The error code of the operation.</param>
    /// <param name="DesiredAccess">The desired access.</param>
    /// <param name="FlagsAndAttributes">The file flags and attributes.</param>
    /// <param name="Path">The path being accessed.</param>
    /// <param name="ProcessArgs">The process arguments.</param>
    /// <param name="IsAnAugmentedFileAccess">Whether the file access is augmented.</param>
    [CLSCompliant(false)]
    public readonly record struct FileAccessData(
        ReportedFileOperation Operation,
        RequestedAccess RequestedAccess,
        uint ProcessId,
        uint Error,
        DesiredAccess DesiredAccess,
        FlagsAndAttributes FlagsAndAttributes,
        string Path,
        string? ProcessArgs,
        bool IsAnAugmentedFileAccess);
}
