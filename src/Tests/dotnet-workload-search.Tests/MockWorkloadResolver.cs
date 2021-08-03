// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Workload.Search.Tests
{
    public class MockWorkloadResolver : IWorkloadResolver
    {
        private readonly IEnumerable<WorkloadResolver.WorkloadInfo> _availableWorkloads;

        public MockWorkloadResolver(IEnumerable<WorkloadResolver.WorkloadInfo> availableWorkloads)
        {
            _availableWorkloads = availableWorkloads;
        }

        public IEnumerable<WorkloadResolver.WorkloadInfo> GetAvailableWorkloads() => _availableWorkloads;

        public IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind) => throw new NotImplementedException();
        public IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public ISet<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packId, out ISet<WorkloadPackId> unsatisfiablePacks) => throw new NotImplementedException();
        public void RefreshWorkloadManifests() => throw new NotImplementedException();
        public WorkloadResolver.PackInfo TryGetPackInfo(WorkloadPackId packId) => throw new NotImplementedException();
        public bool IsPlatformIncompatibleWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetManifestVersion(string manifestId) => throw new NotImplementedException();
        public IEnumerable<WorkloadResolver.ManifestInfo> GetInstalledManifests() => throw new NotImplementedException();
        public IWorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
        public string GetSdkFeatureBand() => throw new NotImplementedException();
        public IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads) => throw new NotImplementedException();
        WorkloadResolver IWorkloadResolver.CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
    }
}
