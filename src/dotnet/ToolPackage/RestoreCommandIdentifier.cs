// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     Given the following parameter, a list of RestoredCommand of a NuGet package can be uniquely identified
    /// </summary>
    internal class RestoreCommandIdentifier : IEquatable<RestoreCommandIdentifier>
    {
        public RestoreCommandIdentifier(
            PackageId packageId,
            NuGetVersion version,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            string commandName)
        {
            PackageId = packageId;
            Version = version ?? throw new ArgumentException(nameof(version));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
            CommandName = commandName ?? throw new ArgumentException(nameof(commandName));
        }

        public PackageId PackageId { get; }
        public NuGetVersion Version { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }
        public string CommandName { get; }  

        public bool Equals(RestoreCommandIdentifier other)
        {
            return other != null &&
                   PackageId.Equals(other.PackageId) &&
                   EqualityComparer<NuGetVersion>.Default.Equals(Version, other.Version) &&
                   EqualityComparer<NuGetFramework>.Default.Equals(TargetFramework, other.TargetFramework) &&
                   string.Equals(
                       RuntimeIdentifier,
                       other.RuntimeIdentifier,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       CommandName,
                       other.CommandName,
                       StringComparison.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RestoreCommandIdentifier);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PackageId, Version, TargetFramework,
                StringComparer.OrdinalIgnoreCase.GetHashCode(RuntimeIdentifier),
                StringComparer.OrdinalIgnoreCase.GetHashCode(CommandName));
        }

        public static bool operator ==(RestoreCommandIdentifier id1, RestoreCommandIdentifier id2)
        {
            return EqualityComparer<RestoreCommandIdentifier>.Default.Equals(id1, id2);
        }

        public static bool operator !=(RestoreCommandIdentifier id1, RestoreCommandIdentifier id2)
        {
            return !(id1 == id2);
        }

        public string DebugToString()
        {
            return
                $"{PackageId}-{Version.ToNormalizedString()}-{TargetFramework.GetShortFolderName()}-{RuntimeIdentifier}-{CommandName}";
        }
    }
}
