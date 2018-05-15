// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using NuGet.Common;

namespace Microsoft.DotNet.Configurer
{
    public class CliFolderPathCalculator
    {
        private const string DotnetProfileDirectoryName = ".dotnet";
        private const string ToolsShimFolderName = "tools";

        public string CliFallbackFolderPath => Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER") ??
                                               Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent.FullName, "NuGetFallbackFolder");
        public string ToolsShimPath => Path.Combine(DotnetUserProfileFolderPath, ToolsShimFolderName);
        public string ToolsPackagePath => ToolPackageFolderPathCalculator.GetToolPackageFolderPath(ToolsShimPath);
        public BashPathUnderHomeDirectory ToolsShimPathInUnix
        {
            get
            {
                return new BashPathUnderHomeDirectory(Environment.GetEnvironmentVariable("HOME"),
                    Path.Combine(DotnetProfileDirectoryName, ToolsShimFolderName));
            }
        }

        public static string DotnetUserProfileFolderPath
        {
            get
            {
                string profileDir = Environment.GetEnvironmentVariable(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");

                return Path.Combine(profileDir, DotnetProfileDirectoryName);
            }
        }

        public string NuGetUserSettingsDirectory =>
            NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
    }
}
