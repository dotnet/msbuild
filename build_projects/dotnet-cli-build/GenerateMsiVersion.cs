// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateMsiVersion : Task
    {
        [Required]
        public int CommitCount { get; set; }

        [Required]
        public int VersionMajor { get; set; }

        [Required]
        public int VersionMinor { get; set; }

        [Required]
        public int VersionPatch { get; set; }

        [Required]
        public string ReleaseSuffix { get; set; }

        [Output]
        public string MsiVersion { get; set; }

        public override bool Execute()
        {
            var buildVersion = new BuildVersion()
            {
                Major = VersionMajor,
                Minor = VersionMinor,
                Patch = VersionPatch,
                ReleaseSuffix = ReleaseSuffix,
                CommitCount = CommitCount
            };

            MsiVersion = buildVersion.GenerateMsiVersion();

            return true;
        }
    }
}
