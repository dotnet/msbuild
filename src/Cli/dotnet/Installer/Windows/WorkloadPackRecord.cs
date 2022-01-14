// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable disable

using System;

using Microsoft.NET.Sdk.WorkloadManifestReader;

using NuGet.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a single workload pack installation record in the registry created
    /// by a workload pack MSI.
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
        /// The ID of the workload pack.
        /// </summary>
        public WorkloadPackId PackId
        {
            get;
            set;
        }

        /// <summary>
        /// The semantic version of the workload pack.
        /// </summary>
        public NuGetVersion PackVersion
        {
            get;
            set;
        }

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
