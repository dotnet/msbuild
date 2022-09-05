// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    class PackageRank
    {
        private Dictionary<string, int> packageRanks;

        public PackageRank(string[] packageIds)
        {
            var numPackages = packageIds?.Length ?? 0;

            // cache ranks for fast lookup
            packageRanks = new Dictionary<string, int>(numPackages, StringComparer.OrdinalIgnoreCase);

            for (int i = numPackages - 1; i >= 0; i--)
            {
                var preferredPackageId = packageIds[i].Trim();

                if (preferredPackageId.Length != 0)
                {
                    // overwrite any duplicates, lowest rank will win.
                    packageRanks[preferredPackageId] = i;
                }
            }
        }

        /// <summary>
        /// Get's the rank of a package, lower packages are preferred
        /// </summary>
        /// <param name="packageId">id of package</param>
        /// <returns>rank of package</returns>
        public int GetPackageRank(string packageId)
        {
            int rank;
            if (packageId != null && packageRanks.TryGetValue(packageId, out rank))
            {
                return rank;
            }

            return int.MaxValue;
        }
    }
}
