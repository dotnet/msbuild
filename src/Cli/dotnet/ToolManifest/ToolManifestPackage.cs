// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal struct ToolManifestPackage : IEquatable<ToolManifestPackage>
    {
        public PackageId PackageId { get; }
        public NuGetVersion Version { get; }
        public ToolCommandName[] CommandNames { get; }
        /// <summary>
        /// The directory that will take effect first.
        /// When it is under .config directory, it is not .config directory
        /// it is .config's parent directory
        /// </summary>
        public DirectoryPath FirstEffectDirectory { get; }

        public ToolManifestPackage(PackageId packagePackageId,
            NuGetVersion version,
            ToolCommandName[] toolCommandNames,
            DirectoryPath firstEffectDirectory)
        {
            FirstEffectDirectory = firstEffectDirectory;
            PackageId = packagePackageId;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            CommandNames = toolCommandNames ?? throw new ArgumentNullException(nameof(toolCommandNames));
        }

        public override bool Equals(object obj)
        {
            return obj is ToolManifestPackage tool &&
                   Equals(tool);
        }

        public bool Equals(ToolManifestPackage other)
        {
            return PackageId.Equals(other.PackageId) &&
                   EqualityComparer<NuGetVersion>.Default.Equals(Version, other.Version) &&
                   CommandNamesEqual(other.CommandNames) &&
                   FirstEffectDirectory.Value.TrimEnd('/', '\\')
                     .Equals(other.FirstEffectDirectory.Value.TrimEnd('/', '\\'), StringComparison.Ordinal);
        }

        private bool CommandNamesEqual(ToolCommandName[] otherCommandNames)
        {
            if (CommandNames == null)
            {
                return otherCommandNames == null;
            }

            if (otherCommandNames == null)
            {
                return false;
            }

            return CommandNames.SequenceEqual(otherCommandNames);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PackageId, Version, CommandNames);
        }

        public static bool operator ==(ToolManifestPackage tool1,
            ToolManifestPackage tool2)
        {
            return tool1.Equals(tool2);
        }

        public static bool operator !=(ToolManifestPackage tool1,
            ToolManifestPackage tool2)
        {
            return !(tool1 == tool2);
        }
    }
}
