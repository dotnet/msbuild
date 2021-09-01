// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    ///  https://github.com/dotnet/sdk/issues/20195
    /// </summary>
    public static class SudoEnvironmentDirectoryOverride
    {
        private const string SudoHomeDirectory = "/tmp/dotnet_sudo_home/";

        /// <summary>
        /// Not for security use. Detect if command is running under sudo
        /// via if SUDO_UID being set.
        /// </summary>
        public static bool IsRunningUnderSudo()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUDO_UID")))
            {
                return true;
            }

            return false;
        }

        public static void OverrideEnvironmentVariableToTmp(ParseResult parseResult)
        {
            if (!OperatingSystem.IsWindows() && IsRunningUnderSudo() && IsRunningWorkloadCommand(parseResult))
            {
                Directory.CreateDirectory(SudoHomeDirectory);
                Environment.SetEnvironmentVariable("HOME", SudoHomeDirectory);
            }
        }

        private static bool IsRunningWorkloadCommand(ParseResult parseResult) =>
            parseResult.RootSubCommandResult() == (WorkloadCommandParser.GetCommand().Name);
    }
}
