// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Deployment.DotNet.Releases;

#nullable disable
namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal struct SdkFeatureBand : IEquatable<SdkFeatureBand>, IComparable<SdkFeatureBand>
    {
        private ReleaseVersion _featureBand;

        public SdkFeatureBand(string version) : this(new ReleaseVersion(version) ?? throw new ArgumentNullException(nameof(version))) { }

        public SdkFeatureBand(ReleaseVersion version)
        {
            var fullVersion = version ?? throw new ArgumentNullException(nameof(version));
            _featureBand = new ReleaseVersion(fullVersion.Major, fullVersion.Minor, fullVersion.SdkFeatureBand);
        }

        public bool Equals(SdkFeatureBand other)
        {
            return _featureBand.Equals(other._featureBand);
        }

        public int CompareTo(SdkFeatureBand other)
        {
            return _featureBand.CompareTo(other._featureBand);
        }

        public override bool Equals(object obj)
        {
            return obj is SdkFeatureBand featureBand && Equals(featureBand);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return _featureBand.ToString();
        }
    }
}
