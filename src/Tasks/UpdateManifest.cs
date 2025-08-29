// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

#nullable disable

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
