// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ReadStaticWebAssetsManifestFile : Task
    {
        [Required]
        public string ManifestPath { get; set; }

        [Output]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        [Output]
        public ITaskItem[] ReferencedProjectsConfiguration { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ManifestPath))
            {
                Log.LogError($"Manifest file at '{ManifestPath}' not found.");
                return false;
            }

            try
            {
                var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath));

                Assets = manifest.Assets?.Select(a => a.ToTaskItem()).ToArray() ?? Array.Empty<ITaskItem>();

                DiscoveryPatterns = manifest.DiscoveryPatterns?.Select(dp => dp.ToTaskItem()).ToArray() ?? Array.Empty<ITaskItem>();

                ReferencedProjectsConfiguration = manifest.ReferencedProjectsConfiguration?.Select(m => m.ToTaskItem()).ToArray() ?? Array.Empty<ITaskItem>();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: ManifestPath);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
