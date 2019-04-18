// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageStore
    {
        DirectoryPath Root { get; }

        DirectoryPath GetRandomStagingDirectory();

        NuGetVersion GetStagedPackageVersion(DirectoryPath stagingDirectory, PackageId packageId);

        DirectoryPath GetRootPackageDirectory(PackageId packageId);

        DirectoryPath GetPackageDirectory(PackageId packageId, NuGetVersion version);
    }
}
