// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.CommandUtils
{
    internal readonly struct CommandResult
    {
        internal CommandResult(ProcessStartInfo startInfo, int exitCode, string? stdOut, string? stdErr)
        {
            StartInfo = startInfo;
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        internal ProcessStartInfo StartInfo { get; }

        internal int ExitCode { get; }

        internal string? StdOut { get; }

        internal string? StdErr { get; }
    }
}
