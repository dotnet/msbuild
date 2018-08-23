// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;
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
