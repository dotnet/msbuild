// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal static partial class ItemUtilities
    {
        public static PackageIdentity GetPackageIdentity(ITaskItem item)
        {
            string packageName = item.GetMetadata(MetadataKeys.NuGetPackageId);
            string packageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);

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
