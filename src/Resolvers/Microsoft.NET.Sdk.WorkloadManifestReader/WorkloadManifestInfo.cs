// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadManifestInfo
    {
        public WorkloadManifestInfo(string id, string version, string manifestDirectory)
        {
            Id = id;
            Version = version;
            ManifestDirectory = manifestDirectory;
            ManifestFeatureBand = Path.GetFileName(Path.GetDirectoryName(manifestDirectory))!;
        }

        public string Id { get; }
        public string Version { get; }
        public string ManifestDirectory { get; }
        public string ManifestFeatureBand { get; }
    }
}
