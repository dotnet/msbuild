// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;

namespace Microsoft.DotNet.Tools.Migrate
{
    public static class ProjectRootElementExtensions
    {
        public static string GetSdkVersion(this ProjectRootElement projectRootElement)
        {
            return projectRootElement
                .Items
                .Where(i => i.ItemType == "PackageReference")
                .First(i => i.Include == SupportedPackageVersions.SdkPackageName)
                .GetMetadataWithName("version").Value;
        }
    }
}
