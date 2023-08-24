// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.FileAccess
{
    /// <summary>
    /// Process data.
    /// </summary>
    /// <param name="ProcessName">The process name.</param>
    /// <param name="ProcessId">The process id.</param>
    /// <param name="ParentProcessId">The parent process id.</param>
    /// <param name="CreationDateTime">The creation date time.</param>
    /// <param name="ExitDateTime">The exit date time.</param>
    /// <param name="ExitCode">The exit code.</param>
    [CLSCompliant(false)]
    public readonly record struct ProcessData(
        string ProcessName,

        uint ProcessId,
        uint ParentProcessId,
        DateTime CreationDateTime,
        DateTime ExitDateTime,
        uint ExitCode);
}
