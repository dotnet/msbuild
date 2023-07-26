// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
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
