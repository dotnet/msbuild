// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// An SDK workload manifest
    /// </summary>
    public class WorkloadManifest
    {
        internal WorkloadManifest(string id, FXVersion version, string? description, string manifestPath,  Dictionary<WorkloadId, BaseWorkloadDefinition> workloads, Dictionary<WorkloadPackId, WorkloadPack> packs, Dictionary<string, FXVersion>? dependsOnManifests)
        {
            Id = id;
            ParsedVersion = version;
            Description = description;
            ManifestPath = manifestPath;
            Workloads = workloads;
            Packs = packs;
            DependsOnManifests = dependsOnManifests;
        }

        /// <summary>
        /// The ID of the manifest is its filename without the extension.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The version of the manifest. It is relative to the SDK band.
        /// </summary>
        public string Version => ParsedVersion.ToString()!;

        /// <summary>
        /// The version of the manifest. It is relative to the SDK band.
        /// </summary>
        internal FXVersion ParsedVersion { get; }

        /// <summary>
        /// ID and minimum version for any other manifests that this manifest depends on. Use only for validating consistancy.
        /// </summary>
        internal Dictionary<string, FXVersion>? DependsOnManifests { get; }

        public string? Description { get; }

        public string ManifestPath { get; }

        public Dictionary<WorkloadId, BaseWorkloadDefinition> Workloads { get; }
        public Dictionary<WorkloadPackId, WorkloadPack> Packs { get; }
    }
}
