// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Build.Framework;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetUseBundledNETCoreAppPackageVersionAsDefaultNetCorePatchVersion : Task
    {
        [Required]
        public string BundledNETCoreAppPackageVersion { get; set; }

        [Output]
        public string UseBundledNETCoreAppPackageVersionAsDefaultNetCorePatchVersion { get; set; }

        public override bool Execute()
        {
            var parsedVersion = NuGetVersion.Parse(BundledNETCoreAppPackageVersion);
            UseBundledNETCoreAppPackageVersionAsDefaultNetCorePatchVersion =
                (parsedVersion.Patch == 0) && parsedVersion.IsPrerelease ? "true" : "false";

            return true;
        }
    }
}
