// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockWorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        public List<SdkFeatureBand> UpdateAdvertisingManifestsCallParams = new List<SdkFeatureBand>();
        public List<SdkFeatureBand> CalculateManifestUpdatesCallParams = new List<SdkFeatureBand>();
        private IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> _manifestUpdates;

        public MockWorkloadManifestUpdater(IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestUpdates = null)
        {
            _manifestUpdates = manifestUpdates ?? new List<(ManifestId, ManifestVersion, ManifestVersion)>();
        }

        public void UpdateAdvertisingManifests(SdkFeatureBand featureBand)
        {
            UpdateAdvertisingManifestsCallParams.Add(featureBand);
        }

        public IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> CalculateManifestUpdates(SdkFeatureBand featureBand)
        {
            CalculateManifestUpdatesCallParams.Add(featureBand);
            return _manifestUpdates;
        }
    }
}
