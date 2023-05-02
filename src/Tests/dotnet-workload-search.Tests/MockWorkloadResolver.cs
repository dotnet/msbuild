// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public IEnumerable<WorkloadResolver.WorkloadInfo> GetExtendedWorkloads(IEnumerable<WorkloadId> workloadIds) => throw new NotImplementedException();

        public ISet<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packId, out ISet<WorkloadPackId> unsatisfiablePacks) => throw new NotImplementedException();
        public void RefreshWorkloadManifests() => throw new NotImplementedException();
        public WorkloadResolver.PackInfo TryGetPackInfo(WorkloadPackId packId) => throw new NotImplementedException();
        public bool IsPlatformIncompatibleWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetManifestVersion(string manifestId) => throw new NotImplementedException();
        public IEnumerable<WorkloadManifestInfo> GetInstalledManifests() => throw new NotImplementedException();
        public IWorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
        public string GetSdkFeatureBand() => "12.0.400";
        public IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads) => throw new NotImplementedException();
        WorkloadResolver IWorkloadResolver.CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
        WorkloadManifest IWorkloadResolver.GetManifestFromWorkload(WorkloadId workloadId) => throw new NotImplementedException();
    }
}
