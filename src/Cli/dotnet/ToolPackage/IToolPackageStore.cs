// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
