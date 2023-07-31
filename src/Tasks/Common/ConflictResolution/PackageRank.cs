// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive


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
