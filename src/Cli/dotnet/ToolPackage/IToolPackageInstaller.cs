// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageInstaller
    {
        IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null);

        IToolPackage InstallPackageToExternalManagedLocation(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null);
    }
}
