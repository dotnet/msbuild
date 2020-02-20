// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
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
