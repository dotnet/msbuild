// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution;

/// <summary>
/// Defines an import from a specific path and that was read at a specified time.
/// </summary>
public record struct ProjectImportInstance
{
    /// <summary>
    /// Constructor of this instance.
    /// </summary>
    /// <param name="fullPath">The full path to the import.</param>
    /// <param name="lastWriteTimeWhenRead">The last-write-time of the file that was read, when it was read.</param>
    public ProjectImportInstance(string fullPath, DateTime lastWriteTimeWhenRead)
    {
        ErrorUtilities.VerifyThrowArgumentNull(fullPath, nameof(fullPath));
        FullPath = fullPath;
        LastWriteTimeWhenRead = lastWriteTimeWhenRead;
    }

    /// <summary>
    /// The full path to the import.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// The last-write-time of the file that was read, when it was read.
    /// This can be used to see whether the file has been changed on disk
    /// by an external means.
    /// </summary>
    public DateTime LastWriteTimeWhenRead { get; }
}
