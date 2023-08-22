// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            if (sdkFeatureBand.Contains("-") || !sdkFeatureBand.EndsWith("00", StringComparison.Ordinal))
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
