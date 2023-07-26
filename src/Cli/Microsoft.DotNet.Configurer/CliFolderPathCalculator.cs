// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Common;

namespace Microsoft.DotNet.Configurer
{
    public static class CliFolderPathCalculator
    {
        public const string DotnetHomeVariableName = CliFolderPathCalculatorCore.DotnetHomeVariableName;
        private const string DotnetProfileDirectoryName = CliFolderPathCalculatorCore.DotnetProfileDirectoryName;
        private const string ToolsShimFolderName = "tools";
        private const string ToolsResolverCacheFolderName = "toolResolverCache";

        public static string CliFallbackFolderPath =>
            Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER") ??
            Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent.FullName, "NuGetFallbackFolder");

        public static string ToolsShimPath => Path.Combine(DotnetUserProfileFolderPath, ToolsShimFolderName);

        public static string ToolsPackagePath =>
            ToolPackageFolderPathCalculator.GetToolPackageFolderPath(ToolsShimPath);

        public static BashPathUnderHomeDirectory ToolsShimPathInUnix =>
            new BashPathUnderHomeDirectory(
                DotnetHomePath,
                Path.Combine(DotnetProfileDirectoryName, ToolsShimFolderName));

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

        public static string PlatformHomeVariableName => CliFolderPathCalculatorCore.PlatformHomeVariableName;

        public static string DotnetHomePath
        {
            get
            {
                return CliFolderPathCalculatorCore.GetDotnetHomePath()
                    ?? throw new ConfigurationException(
                            string.Format(
                                LocalizableStrings.FailedToDetermineUserHomeDirectory,
                                DotnetHomeVariableName))
                        .DisplayAsError();
            }
        }

        public static string NuGetUserSettingsDirectory =>
            NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
    }
}
