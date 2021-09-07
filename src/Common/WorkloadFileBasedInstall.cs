// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.Workloads.Workload
{
    static class WorkloadFileBasedInstall
    {
        public static bool IsUserLocal(string dotnetDir, string sdkFeatureBand)
            => File.Exists(GetUserInstallFilePath(dotnetDir, sdkFeatureBand));

        internal static void SetUserLocal(string dotnetDir, string sdkFeatureBand)
        {
            string filePath = GetUserInstallFilePath(dotnetDir, sdkFeatureBand);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "");
        }

        private static string GetUserInstallFilePath(string dotnetDir, string sdkFeatureBand)
        {
            if (sdkFeatureBand.Contains("-"))
            {
                // The user passed in the sdk version. Derive the feature band version.
                if (!Version.TryParse(sdkFeatureBand.Split('-')[0], out var sdkVersionParsed))
                {
                    throw new FormatException($"'{nameof(sdkFeatureBand)}' should be a version, but get {sdkFeatureBand}");
                }

                static int Last2DigitsTo0(int versionBuild)
                {
                    return (versionBuild / 100) * 100;
                }

                sdkFeatureBand = $"{sdkVersionParsed.Major}.{sdkVersionParsed.Minor}.{Last2DigitsTo0(sdkVersionParsed.Build)}";
            }

            return Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand, "userlocal");
        }
    }
}