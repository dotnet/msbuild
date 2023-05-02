// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// This abstracts out the process of locating and loading a set of manifests to be loaded into a
    /// workload manifest resolver and resolved into a single coherent model.
    /// </summary>
    public interface IWorkloadManifestProvider
    {
        IEnumerable<ReadableWorkloadManifest> GetManifests();

        IEnumerable<string> GetManifestDirectories();

        string GetSdkFeatureBand();
    }
}
