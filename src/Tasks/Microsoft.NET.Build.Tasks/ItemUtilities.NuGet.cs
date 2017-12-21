// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal static partial class ItemUtilities
    {
        public static PackageIdentity GetPackageIdentity(ITaskItem item)
        {
            string packageName = item.GetMetadata(MetadataKeys.PackageName);
            string packageVersion = item.GetMetadata(MetadataKeys.PackageVersion);

            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
            {
                packageName = item.GetMetadata(MetadataKeys.NuGetPackageId);
                packageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);
            }

            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
            {
                return null;
            }

            return new PackageIdentity(
                packageName,
                NuGetVersion.Parse(packageVersion));
        }
    }
}
