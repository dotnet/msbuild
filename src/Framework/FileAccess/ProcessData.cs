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
    public readonly record struct ProcessData(
        string ProcessName,

        // TODO dshepelev: Fix suppression.
#pragma warning disable CS3001, CS3003 // Argument type is not CLS-compliant; Type is not CLS-compliant.
        uint ProcessId,
        uint ParentProcessId,
#pragma warning restore CS3001, CS3003 // Argument type is not CLS-compliant; Type is not CLS-compliant.
        DateTime CreationDateTime,
        DateTime ExitDateTime,

        // TODO dshepelev: Fix suppression.
#pragma warning disable CS3001, CS3003 // Argument type is not CLS-compliant; Type is not CLS-compliant.
        uint ExitCode);
#pragma warning restore CS3001, CS3003 // Argument type is not CLS-compliant; Type is not CLS-compliant.
}
