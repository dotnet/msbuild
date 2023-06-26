// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;

#nullable disable
namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public struct SdkFeatureBand : IEquatable<SdkFeatureBand>, IComparable<SdkFeatureBand>
    {
        private ReleaseVersion _featureBand;

        public SdkFeatureBand(string version) : this(new ReleaseVersion(version) ?? throw new ArgumentNullException(nameof(version))) { }

        public SdkFeatureBand(ReleaseVersion version)
        {
            var fullVersion = version ?? throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrEmpty(version.Prerelease) || version.Prerelease.Contains("dev") || version.Prerelease.Contains("ci"))
            {
                _featureBand = new ReleaseVersion(fullVersion.Major, fullVersion.Minor, fullVersion.SdkFeatureBand);
            }
            else
            {
                // Treat preview versions as their own feature bands
                var prereleaseComponents = fullVersion.Prerelease.Split('.');
                var formattedPrerelease = prereleaseComponents.Length > 1 ? 
                    $"{prereleaseComponents[0]}.{prereleaseComponents[1]}"
                    : prereleaseComponents[0];
                _featureBand = new ReleaseVersion(fullVersion.Major, fullVersion.Minor, fullVersion.SdkFeatureBand, formattedPrerelease);
            }
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

        public string ToStringWithoutPrerelease()
        {
            return new ReleaseVersion(_featureBand.Major, _featureBand.Minor, _featureBand.SdkFeatureBand).ToString();
        }

        public static bool operator >(SdkFeatureBand a, SdkFeatureBand b) => a.CompareTo(b) > 0;

        public static bool operator <(SdkFeatureBand a, SdkFeatureBand b) => a.CompareTo(b) < 0;
    }
}
