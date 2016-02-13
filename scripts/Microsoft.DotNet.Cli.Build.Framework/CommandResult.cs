// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    public struct CommandResult
    {
        public static readonly CommandResult Empty = new CommandResult();

        public ProcessStartInfo StartInfo { get; }
        public int ExitCode { get; }
        public string StdOut { get; }
        public string StdErr { get; }

        public CommandResult(ProcessStartInfo startInfo, int exitCode, string stdOut, string stdErr)
        {
            StartInfo = startInfo;
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public void EnsureSuccessful()
        {
            if(ExitCode != 0)
            {
                throw new BuildFailureException($"Command failed with exit code {ExitCode}: {StartInfo.FileName} {StartInfo.Arguments}");
            }
        }
    }
}