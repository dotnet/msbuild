// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.FileAccess
{
    /*
     * Implementation note: This is a copy of BuildXL.Processes.RequestedAccess.
     * The purpose of the copy is because this is part of the public MSBuild API and it's not desirable to
     * expose BuildXL types directly.
     */

    /// <summary>
    /// Level of access requested by a reported file operation.
    /// </summary>
    [Flags]
    public enum RequestedAccess : byte
    {
        /// <summary>
        /// No access requested.
        /// </summary>
        None = 0,

        /// <summary>
        /// Read access requested.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write access requested.
        /// </summary>
        Write = 2,

        /// <summary>
        /// Metadata-only probe access requested (e.g. <see cref="ReportedFileOperation.GetFileAttributes"/>).
        /// </summary>
        Probe = 4,

        /// <summary>
        /// Directory enumeration access requested (on the directory itself; immediate children will be enumerated).
        /// </summary>
        Enumerate = 8,

        /// <summary>
        /// Metadata-only probe access requested; probed as part of a directory enumeration (e.g. <see cref="ReportedFileOperation.FindNextFile"/>).
        /// </summary>
        EnumerationProbe = 16,

        /// <summary>
        /// Both read and write access requested.
        /// </summary>
        ReadWrite = Read | Write,

        /// <summary>
        /// All defined access levels requested.
        /// </summary>
        All = Read | Write | Probe | Enumerate | EnumerationProbe,
    }
}
