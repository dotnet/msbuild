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

        public SdkFeatureBand(string featureBand)
        {
            _featureBand = new ReleaseVersion(featureBand) ?? throw new ArgumentNullException(nameof(featureBand));
        }

        public bool Equals(SdkFeatureBand other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(SdkFeatureBand other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
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
