// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// An enumeration describing the requests the install client can
    /// send to the elevated server. Each requests describes an operaiton that
    /// requires elevation.
    /// </summary>
    public enum InstallRequestType
    {
        /// <summary>
        /// Requests the server to shutdown.
        /// </summary>
        Shutdown = 0,

        /// <summary>
        /// Request an MSI payload to be cached.
        /// </summary>
        CachePayload = 100,

        /// <summary>
        /// Add a dependent to the provider key of an MSI.
        /// </summary>
        AddDependent = 200,

        /// <summary>
        /// Remove a dependent from an MSI's provider key;
        /// </summary>
        RemoveDependent,

        /// <summary>
        /// Install an MSI.
        /// </summary>
        InstallMsi = 300,

        /// <summary>
        /// Uninstall an MSI.
        /// </summary>
        UninstallMsi,

        /// <summary>
        /// Repair an MSI.
        /// </summary>
        RepairMsi,

        /// <summary>
        /// Create a workload installation record.
        /// </summary>
        WriteWorkloadInstallationRecord = 400,

        /// <summary>
        /// Remove a workload installation record.
        /// </summary>
        DeleteWorkloadInstallationRecord
    }
}
