// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Workload.Search.Tests
{
    public class MockWorkloadResolver : IWorkloadResolver
    {
        private readonly IEnumerable<WorkloadDefinition> _avaliableWorkloads;

        public MockWorkloadResolver(IEnumerable<WorkloadDefinition> avaliableWorkloads)
        {
            _avaliableWorkloads = avaliableWorkloads;
        }

        public IEnumerable<WorkloadDefinition> GetAvaliableWorkloads()
        {
            return _avaliableWorkloads;
        }

        public IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind) => throw new NotImplementedException();
        public IEnumerable<string> GetPacksInWorkload(string workloadId) => throw new NotImplementedException();
        public ISet<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<string> packId) => throw new NotImplementedException();
        public void RefreshWorkloadManifests() => throw new NotImplementedException();
        public WorkloadResolver.PackInfo TryGetPackInfo(string packId) => throw new NotImplementedException();
        public WorkloadResolver CreateTempDirResolver(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion) => throw new NotImplementedException();
        public bool IsWorkloadPlatformCompatible(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetManifestVersion(string manifestId) => throw new NotImplementedException();
    }
}
