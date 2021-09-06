// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            => Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand, "userlocal");
    }
}