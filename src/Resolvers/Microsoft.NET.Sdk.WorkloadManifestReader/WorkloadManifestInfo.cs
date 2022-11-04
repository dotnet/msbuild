// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
