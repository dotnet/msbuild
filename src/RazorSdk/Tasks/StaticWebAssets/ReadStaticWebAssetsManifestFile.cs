// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        public ITaskItem[] RelatedManifests { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ManifestPath))
            {
                Log.LogError($"Manifest file at '{ManifestPath}' not found.");
            }

            try
            {
                var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath));

                // When we are reading a publish manifest we are about to compute the list of files to publish
                // so we filter out files marked as Reference here since they don't have to be copied to the output.
                // The process for merging assets from dependent projects reads their manifests directly, so its not
                // an issue there.
                var isPublishManifest = string.Equals(manifest.ManifestType, StaticWebAssetsManifest.ManifestTypes.Publish, StringComparison.Ordinal);
                if (isPublishManifest)
                {
                    var assets = new List<StaticWebAsset>();
                    foreach (var asset in manifest.Assets)
                    {
                        if (string.Equals(asset.SourceId, manifest.Source, StringComparison.Ordinal) &&
                            string.Equals(asset.AssetMode, StaticWebAsset.AssetModes.Reference))
                        {
                            continue;
                        }

                        assets.Add(asset);
                    }

                    Assets = assets.Select(a => a.ToTaskItem()).ToArray();
                }
                else
                {
                    Assets = manifest.Assets.Select(a => a.ToTaskItem()).ToArray();
                }

                RelatedManifests = manifest.RelatedManifests.Select(m => m.ToTaskItem()).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}
