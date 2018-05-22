// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Updates selected properties in a manifest and resigns.
    /// </summary>
    public class UpdateManifest : Task
    {
        [Required]
        public string ApplicationPath { get; set; }

        public string TargetFrameworkVersion { get; set; }

        [Required]
        public ITaskItem ApplicationManifest { get; set; }

        [Required]
        public ITaskItem InputManifest { get; set; }

        [Output]
        public ITaskItem OutputManifest { get; set; }

        public override bool Execute()
        {
            Manifest.UpdateEntryPoint(InputManifest.ItemSpec, OutputManifest.ItemSpec, ApplicationPath, ApplicationManifest.ItemSpec, TargetFrameworkVersion);

            return true;
        }
    }
}
