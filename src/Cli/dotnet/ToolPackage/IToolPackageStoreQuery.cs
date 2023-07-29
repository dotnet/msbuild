// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageStoreQuery
    {
        IEnumerable<IToolPackage> EnumeratePackages();

        IEnumerable<IToolPackage> EnumeratePackageVersions(PackageId packageId);

        IToolPackage GetPackage(PackageId packageId, NuGetVersion version);
    }
}
