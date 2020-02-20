// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     A range of RestoredCommandIdentifier that is only different in the Version field.
    /// </summary>
    internal class RestoredCommandIdentifierVersionRange
    {
        public RestoredCommandIdentifierVersionRange(
            PackageId packageId,
            VersionRange versionRange,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            ToolCommandName commandName)
        {
            PackageId = packageId;
            VersionRange = versionRange ?? throw new ArgumentException(nameof(versionRange));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
            CommandName = commandName;
        }

        public PackageId PackageId { get; }
        public VersionRange VersionRange { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }
        public ToolCommandName CommandName { get; }

        public RestoredCommandIdentifier WithVersion(NuGetVersion version)
        {
            return new RestoredCommandIdentifier(PackageId, version, TargetFramework, RuntimeIdentifier, CommandName);
        }
    }
}
