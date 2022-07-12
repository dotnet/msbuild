// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using NuGet.Common;
using NuGet.Configuration;

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
                if (!TempHomeIsOnlyRootWritable(SudoHomeDirectory))
                {
                    try
                    {
                        Directory.Delete(SudoHomeDirectory, recursive: true);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Avoid read after write race condition
                    }
                }

                Directory.CreateDirectory(SudoHomeDirectory);

                var homeBeforeOverride = Path.Combine(Environment.GetEnvironmentVariable("HOME"));
                Environment.SetEnvironmentVariable("HOME", SudoHomeDirectory);

                CopyUserNuGetConfigToOverriddenHome(homeBeforeOverride);
            }
        }

        /// <summary>
        /// To make NuGet honor the user's NuGet config file.
        /// Copying instead of using the file directoy to avoid existing file being set higher permission
        /// Try to delete the existing NuGet config file in "/tmp/dotnet_sudo_home/"
        /// to avoid different user's NuGet config getting mixed.
        /// </summary>
        private static void CopyUserNuGetConfigToOverriddenHome(string homeBeforeOverride)
        {
            // https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Common/PathUtil/NuGetEnvironment.cs#L139
            // home is cache in NuGet we cannot directly use the call
            var userSettingsDir = Path.Combine(homeBeforeOverride, ".nuget", "NuGet");

            string userNuGetConfig = Settings.OrderedSettingsFileNames
                .Select(fileName => Path.Combine(userSettingsDir, fileName))
                .FirstOrDefault(f => File.Exists(f));

            var overridenSettingsDir = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            var overridenNugetConfig = Path.Combine(overridenSettingsDir, Settings.DefaultSettingsFileName);

            if (File.Exists(overridenNugetConfig))
            {
                try
                {
                    FileAccessRetrier.RetryOnIOException(
                        () => File.Delete(overridenNugetConfig));
                }
                catch
                {
                    // best effort to remove
                }
            }

            if (userNuGetConfig != default)
            {
                try
                {
                    FileAccessRetrier.RetryOnIOException(
                        () => File.Copy(userNuGetConfig, overridenNugetConfig, overwrite: true));
                }
                catch
                {
                    // best effort to copy
                }
            }
        }

        private static bool IsRunningWorkloadCommand(ParseResult parseResult) =>
            parseResult.RootSubCommandResult() == (WorkloadCommandParser.GetCommand().Name);

        private static bool TempHomeIsOnlyRootWritable(string path)
        {
            if (StatInterop.LStat(path, out StatInterop.FileStatus fileStat) != 0)
            {
                return false;
            }

            return IsOwnedByRoot(fileStat) && GroupCannotWrite(fileStat) &&
                   OtherUserCannotWrite(fileStat);
        }

        private static bool OtherUserCannotWrite(StatInterop.FileStatus fileStat)
        {
            return (fileStat.Mode & (int) StatInterop.Permissions.S_IWOTH) == 0;
        }

        private static bool GroupCannotWrite(StatInterop.FileStatus fileStat)
        {
            return (fileStat.Mode & (int) StatInterop.Permissions.S_IWGRP) == 0;
        }

        private static bool IsOwnedByRoot(StatInterop.FileStatus fileStat)
        {
            return fileStat.Uid == 0;
        }
    }
}
