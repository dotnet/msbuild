// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ShellShim
{
    internal static class ZshDetector
    {
        private const string ZshFileName = "zsh";
        /// <summary>
        /// Returns true if the `SHELL` environment variable ends with `zsh` for the filename.
        /// </summary>
        public static bool IsZshTheUsersShell(IEnvironmentProvider environmentProvider)
        {
            string environmentVariable = environmentProvider.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(environmentVariable))
            {
                return false;
            }

            if (Path.GetFileName(environmentVariable).Equals(ZshFileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
