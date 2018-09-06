// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     A range of CommandSettingsListId that is only different in the Version field.
    /// </summary>
    internal class CommandSettingsListIdVersionRange
    {
        public CommandSettingsListIdVersionRange(
            PackageId packageId,
            VersionRange versionRange,
            NuGetFramework targetFramework,
            string runtimeIdentifier)
        {
            PackageId = packageId;
            VersionRange = versionRange ?? throw new ArgumentException(nameof(versionRange));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
        }

        public PackageId PackageId { get; }
        public VersionRange VersionRange { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }

        public CommandSettingsListId WithVersion(NuGetVersion version)
        {
            return new CommandSettingsListId(PackageId, version, TargetFramework, RuntimeIdentifier);
        }
    }
}
