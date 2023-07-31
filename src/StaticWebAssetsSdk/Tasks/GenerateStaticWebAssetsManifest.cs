// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
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
        public ITaskItem[] ReferencedProjectsConfigurations { get; set; }

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

                var assetsByTargetPath = assets.GroupBy(a => a.ComputeTargetPath("", '/'), StringComparer.OrdinalIgnoreCase);
                foreach (var group in assetsByTargetPath)
                {
                    if (!StaticWebAsset.ValidateAssetGroup(group.Key, group.ToArray(), out var reason))
                    {
                        Log.LogError(reason);
                        return false;
                    }
                }

                var discoveryPatterns = DiscoveryPatterns
                    .OrderBy(a => a.ItemSpec)
                    .Select(StaticWebAssetsDiscoveryPattern.FromTaskItem)
                    .ToArray();

                var referencedProjectsConfiguration = ReferencedProjectsConfigurations.OrderBy(a => a.ItemSpec)
                    .Select(StaticWebAssetsManifest.ReferencedProjectConfiguration.FromTaskItem)
                    .ToArray();

                PersistManifest(
                    StaticWebAssetsManifest.Create(
                        Source,
                        BasePath,
                        Mode,
                        ManifestType,
                        referencedProjectsConfiguration,
                        discoveryPatterns,
                        assets.ToArray()));
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail:true, file: null);
            }
            return !Log.HasLoggedErrors;
        }

        private void PersistManifest(StaticWebAssetsManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestSerializationOptions);
            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ? StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath)).Hash : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest because manifest file '{ManifestPath}' does not exist.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!string.Equals(manifest.Hash, existingManifestHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest version '{manifest.Hash}' is different from existing manifest hash '{existingManifestHash}'.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest version '{manifest.Hash}' has not changed.");
            }
        }
    }
}
