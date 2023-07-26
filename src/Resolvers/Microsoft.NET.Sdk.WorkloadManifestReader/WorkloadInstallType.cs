// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Describes different workload installation types.
    /// </summary>
    public enum InstallType
    {
        /// <summary>
        /// Workloads are installed as NuGet packages
        /// </summary>
        FileBased = 0,
        /// <summary>
        /// Workloads are installed as MSIs.
        /// </summary>
        Msi = 1
    }

    public static class WorkloadInstallType
    {
        /// <summary>
        /// Determines the <see cref="InstallType"/> associated with a specific SDK version.
        /// </summary>
        /// <param name="sdkFeatureBand">The SDK version to check.</param>
        /// <returns>The <see cref="InstallType"/> associated with the SDK.</returns>
        public static InstallType GetWorkloadInstallType(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            string installerTypePath = Path.Combine(dotnetDir, "metadata",
                "workloads", $"{sdkFeatureBand.ToStringWithoutPrerelease()}", "installertype");

            if (File.Exists(Path.Combine(installerTypePath, "msi")))
            {
                return InstallType.Msi;
            }

            return InstallType.FileBased;
        }

        public static string GetInstallStateFolder(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            var installType = GetWorkloadInstallType(sdkFeatureBand, dotnetDir);

            if (installType == InstallType.FileBased)
            {
                return Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand.ToString(), "InstallState");
            }
            else if (installType == InstallType.Msi)
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "dotnet", "workloads", sdkFeatureBand.ToString(), "InstallState");
            }
            else
            {
                throw new ArgumentException("Unexpected InstallType: " + installType);
            }
        }
    }
}
