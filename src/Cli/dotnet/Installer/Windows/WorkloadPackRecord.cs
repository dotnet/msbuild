// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

using NuGet.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a workload pack installation record in the registry created
    /// by a workload pack MSI.  This may represent either an individual workload pack
    /// MSI, or a workload pack group MSI which is a single MSI that installs multiple
    /// workload packs.
    /// </summary>
    internal class WorkloadPackRecord
    {
        /// <summary>
        /// The dependency provider key of the workload pack MSI used for reference counting shared installations.
        /// </summary>
        public string ProviderKeyName
        {
            get;
            set;
        }

        /// <summary>
        /// An identifier for the MSI.  If this is an MSI for a single workload pack, it will be the ID of the pack.
        /// If it's an MSI with multiple workload packs (a workload pack group), then the ID will be the ID of the group.
        /// This ID does NOT include the ".Msi.{HostArchitecture}" suffix
        /// </summary>
        public string MsiId
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the workload pack installed by this MSI, or the version of the group of packs
        /// </summary>
        public string MsiNuGetVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The workload pack IDs and versions that are installed by this MSI
        /// </summary>
        public List<(WorkloadPackId id, NuGetVersion version)> InstalledPacks { get; set; } = new();

        /// <summary>
        /// The product code (GUID) of the workload pack MSI.
        /// </summary>
        public string ProductCode
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the workload pack MSI.
        /// </summary>
        public Version ProductVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The upgrade code (GUID) of the workload pack MSI.
        /// </summary>
        public string UpgradeCode
        {
            get;
            set;
        }
    }
}
