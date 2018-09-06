// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     Given the following parameter, a list of CommandSettings of a NuGet package can be uniquely identified
    /// </summary>
    internal class CommandSettingsListId : IEquatable<CommandSettingsListId>
    {
        public CommandSettingsListId(
            PackageId packageId,
            NuGetVersion version,
            NuGetFramework targetFramework,
            string runtimeIdentifier)
        {
            PackageId = packageId;
            Version = version ?? throw new ArgumentException(nameof(version));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
        }

        public PackageId PackageId { get; }
        public NuGetVersion Version { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }

        public bool Equals(CommandSettingsListId other)
        {
            return other != null &&
                   PackageId.Equals(other.PackageId) &&
                   EqualityComparer<NuGetVersion>.Default.Equals(Version, other.Version) &&
                   EqualityComparer<NuGetFramework>.Default.Equals(TargetFramework, other.TargetFramework) &&
                   string.Equals(
                       RuntimeIdentifier,
                       other.RuntimeIdentifier,
                       StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CommandSettingsListId);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PackageId, Version, TargetFramework,
                StringComparer.OrdinalIgnoreCase.GetHashCode(RuntimeIdentifier));
        }

        public static bool operator ==(CommandSettingsListId id1, CommandSettingsListId id2)
        {
            return EqualityComparer<CommandSettingsListId>.Default.Equals(id1, id2);
        }

        public static bool operator !=(CommandSettingsListId id1, CommandSettingsListId id2)
        {
            return !(id1 == id2);
        }

        public string DebugToString()
        {
            return
                $"{PackageId}-{Version.ToNormalizedString()}-{TargetFramework.GetShortFolderName()}-{RuntimeIdentifier}";
        }
    }
}
