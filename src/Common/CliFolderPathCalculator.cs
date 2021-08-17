// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
// using Microsoft.DotNet.Cli.Utils;
// using NuGet.Common;

namespace Microsoft.DotNet.Configurer
{
    static class CliFolderPathCalculator
    {
        public const string DotnetHomeVariableName = "DOTNET_CLI_HOME";
        public const string DotnetProfileDirectoryName = ".dotnet";
        public const string ToolsShimFolderName = "tools";
        private const string ToolsResolverCacheFolderName = "toolResolverCache";

        public static string CliFallbackFolderPath =>
            Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER") ??
            Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent!.FullName, "NuGetFallbackFolder");

        public static string ToolsShimPath => Path.Combine(DotnetUserProfileFolderPath, ToolsShimFolderName);

        public static string ToolsPackagePath =>
            ToolPackageFolderPathCalculator.GetToolPackageFolderPath(ToolsShimPath);

        public static string WindowsNonExpandedToolsShimPath
        {
            get
            {
                return string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DotnetHomeVariableName))
                    ? $@"%USERPROFILE%\{DotnetProfileDirectoryName}\{ToolsShimFolderName}"
                    : ToolsShimPath;
            }
        }

        public static string DotnetUserProfileFolderPath =>
            Path.Combine(DotnetHomePath, DotnetProfileDirectoryName);

        public static string ToolsResolverCachePath => Path.Combine(DotnetUserProfileFolderPath, ToolsResolverCacheFolderName);

        public static string PlatformHomeVariableName =>
            IsWindows ? "USERPROFILE" : "HOME";

        public static string DotnetHomePath
        {
            get
            {
                var home = Environment.GetEnvironmentVariable(DotnetHomeVariableName);
                if (string.IsNullOrEmpty(home))
                {
                    home = Environment.GetEnvironmentVariable(PlatformHomeVariableName);
                    if (string.IsNullOrEmpty(home))
                    {
                        throw new Exception(); // TODO
                        // throw new ConfigurationException(
                        //         string.Format(
                        //             LocalizableStrings.FailedToDetermineUserHomeDirectory,
                        //             DotnetHomeVariableName))
                        //     .DisplayAsError();
                    }
                }

                return home;
            }
        }

        private static bool IsWindows => Path.DirectorySeparatorChar == '\\';
    }

    static class ToolPackageFolderPathCalculator
    {
        private const string NestedToolPackageFolderName = ".store";
        public static string GetToolPackageFolderPath(string toolsShimPath)
        {
            return Path.Combine(toolsShimPath, NestedToolPackageFolderName);
        }
    }
}
