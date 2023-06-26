// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Workloads.Workload
{
    /// <summary>
    /// Describes different workload installation types.
    /// </summary>
    internal enum InstallType
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
}
