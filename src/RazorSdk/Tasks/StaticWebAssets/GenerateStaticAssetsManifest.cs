// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateStaticWebAssetsManifest : Task
    {
        // Since the manifest is only used at development time, it's ok for it to use the relaxed
        // json escaping (which is also what MVC uses by default) and to produce indented output
        // since that makes it easier to inspect the manifest when necessary.
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        [Required]
        public string Source { get; set; }

        [Required]
        public string BasePath { get; set; }

        [Required]
        public string Mode { get; set; }

        [Required]
        public string ManifestType { get; set; }

        [Required]
        public ITaskItem[] RelatedManifests { get; set; }

        [Required]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string ManifestPath { get; set; }

        public override bool Execute()
        {
            try
            {
                var assets = Assets.OrderBy(a => a.GetMetadata("FullPath")).Select(StaticWebAsset.FromTaskItem);

                var relatedManifests = RelatedManifests.OrderBy(a => a.GetMetadata("FullPath"))
                    .Select(ComputeManifestReference)
                    .Where(r => r != null)
                    .ToArray();

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                var discoveryPatterns = DiscoveryPatterns
                    .OrderBy(a => a.ItemSpec)
                    .Select(ComputeDiscoveryPattern)
                    .ToArray();

                PersistManifest(
                    StaticWebAssetsManifest.Create(
                        Source,
                        BasePath,
                        Mode,
                        ManifestType,
                        relatedManifests,
                        discoveryPatterns,
                        assets.ToArray()));
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                Log.LogErrorFromException(ex);
            }
            return !Log.HasLoggedErrors;
        }

        private StaticWebAssetsManifest.DiscoveryPattern ComputeDiscoveryPattern(ITaskItem pattern)
        {
            var name = pattern.ItemSpec;
            var contentRoot = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.ContentRoot));
            var basePath = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.BasePath));
            var glob = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.Pattern));

            return StaticWebAssetsManifest.DiscoveryPattern.Create(name, contentRoot, basePath, glob);
        }

        private StaticWebAssetsManifest.ManifestReference ComputeManifestReference(ITaskItem reference)
        {
            var identity = reference.GetMetadata("FullPath");
            var source = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.Source));
            var manifestType = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ManifestType));
            var projectFile = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ProjectFile));
            var publishTarget = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.PublishTarget));
            var additionalPublishProperties = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.AdditionalPublishProperties));
            var additionalPublishPropertiesToRemove = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.AdditionalPublishPropertiesToRemove));

            if (!File.Exists(identity))
            {
                if (!StaticWebAssetsManifest.ManifestTypes.IsPublish(manifestType))
                {
                    Log.LogError("Manifest '{0}' for project '{1}' with type '{2}' does not exist.", identity, source, manifestType);
                    return null;
                }

                var publishManifest = StaticWebAssetsManifest.ManifestReference.Create(identity, source, manifestType, projectFile, "");
                publishManifest.PublishTarget = publishTarget;
                publishManifest.AdditionalPublishProperties = additionalPublishProperties;
                publishManifest.AdditionalPublishPropertiesToRemove = additionalPublishPropertiesToRemove;

                return publishManifest;
            }

            var relatedManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(identity));

            var result = StaticWebAssetsManifest.ManifestReference.Create(identity, source, manifestType, projectFile, relatedManifest.Hash);
            result.PublishTarget = publishTarget;
            result.AdditionalPublishProperties = additionalPublishProperties;
            result.AdditionalPublishPropertiesToRemove = additionalPublishPropertiesToRemove;

            return result;
        }

        private void PersistManifest(StaticWebAssetsManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestSerializationOptions);
            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ? StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath)).Hash : "";

            if (!fileExists)
            {
                Log.LogMessage($"Creating manifest because manifest file '{ManifestPath}' does not exist.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!string.Equals(manifest.Hash, existingManifestHash, StringComparison.Ordinal))
            {
                Log.LogMessage($"Updating manifest because manifest version '{manifest.Hash}' is different from existing manifest hash '{existingManifestHash}'.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage($"Skipping manifest updated because manifest version '{manifest.Hash}' has not changed.");
            }
        }
    }
}
