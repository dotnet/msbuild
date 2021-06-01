// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
