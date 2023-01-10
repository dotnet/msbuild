// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class ManifestVersionUpdate : IEquatable<ManifestVersionUpdate>, IComparable<ManifestVersionUpdate>
    {
        public ManifestVersionUpdate(ManifestId manifestId, ManifestVersion? existingVersion, string? existingFeatureBand, ManifestVersion? newVersion, string? newFeatureBand)
        {
            ManifestId = manifestId;
            ExistingVersion = existingVersion;
            ExistingFeatureBand = existingFeatureBand;
            NewVersion = newVersion;
            NewFeatureBand = newFeatureBand;
        }

        public ManifestId ManifestId { get; }
        public ManifestVersion? ExistingVersion { get; }
        public string? ExistingFeatureBand { get; }
        public ManifestVersion? NewVersion { get; }
        public string? NewFeatureBand { get; }

        //  Returns an object representing an undo of this manifest update
        public ManifestVersionUpdate Reverse()
        {
            return new ManifestVersionUpdate(ManifestId, NewVersion, NewFeatureBand, ExistingVersion, ExistingFeatureBand);
        }

        public int CompareTo(ManifestVersionUpdate? other)
        {
            if (other == null) return 1;
            int ret = ManifestId.CompareTo(other.ManifestId);
            if (ret != 0) return ret;
            
            if (ExistingVersion == null && other.ExistingVersion != null) return -1;
            if (ExistingVersion != null && other.ExistingVersion == null) return 1;
            if (ExistingVersion != null)
            {
                ret = ExistingVersion.CompareTo(other.ExistingVersion);
                if (ret != 0) return ret;
            }

            ret = string.Compare(ExistingFeatureBand, other.ExistingFeatureBand, StringComparison.Ordinal);
            if (ret != 0) return ret;

            if (NewVersion == null && other.NewVersion != null) return -1;
            if (NewVersion != null && other.NewVersion == null) return 1;
            if (NewVersion != null)
            {
                ret = NewVersion.CompareTo(other.NewVersion);
                if (ret != 0) return ret;
            }

            ret = string.Compare(NewFeatureBand, other.NewFeatureBand, StringComparison.Ordinal);
            return ret;
        }
        public bool Equals(ManifestVersionUpdate? other)
        {
            if (other == null) return false;
            return EqualityComparer<ManifestId>.Default.Equals(ManifestId, other.ManifestId) &&
                EqualityComparer<ManifestVersion?>.Default.Equals(ExistingVersion, other.ExistingVersion) &&
                string.Equals(ExistingFeatureBand, other.ExistingFeatureBand, StringComparison.Ordinal) &&
                EqualityComparer<ManifestVersion?>.Default.Equals(NewVersion, other.NewVersion) &&
                string.Equals(NewFeatureBand, other.NewFeatureBand, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestVersionUpdate id && Equals(id);
        }

        public override int GetHashCode()
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(ManifestId, ExistingVersion, ExistingFeatureBand, NewVersion, NewFeatureBand);
#else
            int hashCode = 1601069575;
            hashCode = hashCode * -1521134295 + ManifestId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestVersion?>.Default.GetHashCode(ExistingVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(ExistingFeatureBand);
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestVersion?>.Default.GetHashCode(NewVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(NewFeatureBand);
            return hashCode;
#endif
        }
    }
}
