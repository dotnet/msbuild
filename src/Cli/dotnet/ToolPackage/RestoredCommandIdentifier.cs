// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    /// <summary>
    ///     Given the following parameter, a list of RestoredCommand of a NuGet package can be uniquely identified
    /// </summary>
    internal class RestoredCommandIdentifier : IEquatable<RestoredCommandIdentifier>
    {
        public RestoredCommandIdentifier(
            PackageId packageId,
            NuGetVersion version,
            NuGetFramework targetFramework,
            string runtimeIdentifier,
            ToolCommandName commandName)
        {
            PackageId = packageId;
            Version = version ?? throw new ArgumentException(nameof(version));
            TargetFramework = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
            RuntimeIdentifier = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
            CommandName = commandName;
        }

        public PackageId PackageId { get; }
        public NuGetVersion Version { get; }
        public NuGetFramework TargetFramework { get; }
        public string RuntimeIdentifier { get; }
        public ToolCommandName CommandName { get; }

        public bool Equals(RestoredCommandIdentifier other)
        {
            return other != null &&
                   PackageId.Equals(other.PackageId) &&
                   EqualityComparer<NuGetVersion>.Default.Equals(Version, other.Version) &&
                   EqualityComparer<NuGetFramework>.Default.Equals(TargetFramework, other.TargetFramework) &&
                   string.Equals(
                       RuntimeIdentifier,
                       other.RuntimeIdentifier,
                       StringComparison.OrdinalIgnoreCase) &&
                   CommandName.Equals(
                       other.CommandName);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RestoredCommandIdentifier);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PackageId, Version, TargetFramework, CommandName,
                StringComparer.OrdinalIgnoreCase.GetHashCode(RuntimeIdentifier));
        }

        public static bool operator ==(RestoredCommandIdentifier id1, RestoredCommandIdentifier id2)
        {
            return EqualityComparer<RestoredCommandIdentifier>.Default.Equals(id1, id2);
        }

        public static bool operator !=(RestoredCommandIdentifier id1, RestoredCommandIdentifier id2)
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
