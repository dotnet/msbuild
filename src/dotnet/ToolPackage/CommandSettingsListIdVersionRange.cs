// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     A range of RestoreCommandIdentifier that is only different in the Version field.
    /// </summary>
    internal class CommandSettingsListIdVersionRange
    {
        public CommandSettingsListIdVersionRange(
            PackageId packageId,
            VersionRange versionRange,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            string commandName)
        {
            PackageId = packageId;
            VersionRange = versionRange ?? throw new ArgumentException(nameof(versionRange));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
            CommandName = commandName ?? throw new ArgumentException(nameof(commandName));
        }

        public PackageId PackageId { get; }
        public VersionRange VersionRange { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }
        public string CommandName { get; }

        public RestoreCommandIdentifier WithVersion(NuGetVersion version)
        {
            return new RestoreCommandIdentifier(PackageId, version, TargetFramework, RuntimeIdentifier, CommandName);
        }
    }
}
