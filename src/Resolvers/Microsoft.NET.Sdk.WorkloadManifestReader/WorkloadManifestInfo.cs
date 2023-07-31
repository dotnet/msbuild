// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadManifestInfo
    {
        public WorkloadManifestInfo(string id, string version, string manifestDirectory, string manifestFeatureBand)
        {
            Id = id;
            Version = version;
            ManifestDirectory = manifestDirectory;
            ManifestFeatureBand = manifestFeatureBand;
        }

        public string Id { get; }
        public string Version { get; }
        public string ManifestDirectory { get; }
        public string ManifestFeatureBand { get; }
    }
}
