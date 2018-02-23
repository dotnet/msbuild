// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageInstaller
    {
        IToolPackage InstallPackage(
            PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            FilePath? nugetConfig = null,
            string source = null,
            string verbosity = null);
    }
}
