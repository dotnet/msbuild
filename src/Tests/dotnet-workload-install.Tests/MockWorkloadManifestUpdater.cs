// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockWorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        public int UpdateAdvertisingManifestsCallCount = 0;
        public int CalculateManifestUpdatesCallCount = 0;
        private IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> _manifestUpdates;

        public MockWorkloadManifestUpdater(IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestUpdates = null)
        {
            _manifestUpdates = manifestUpdates ?? new List<(ManifestId, ManifestVersion, ManifestVersion)>();
        }

        public Task UpdateAdvertisingManifestsAsync(bool includePreview)
        {
            UpdateAdvertisingManifestsCallCount++;
            return Task.CompletedTask;
        }

        public IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> CalculateManifestUpdates()
        {
            CalculateManifestUpdatesCallCount++;
            return _manifestUpdates;
        }
    }
}
