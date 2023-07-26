// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class StaticWebAssetsReadPackManifest : Task
    {
        [Required]
        public string ManifestPath { get; set; }

        [Output] public ITaskItem[] Files { get; set; }

        [Output] public ITaskItem[] AdditionalElementsToRemoveFromPacking { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ManifestPath))
            {
                Log.LogError($"Manifest file at '{ManifestPath}' not found.");
                return false;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<StaticWebAssetsPackManifest>(File.ReadAllBytes(ManifestPath));
                Files = manifest.Files.Select(ToTaskItem).ToArray();
                AdditionalElementsToRemoveFromPacking = manifest.ElementsToRemove?.Select(e => new TaskItem(e)).ToArray() ?? Array.Empty<ITaskItem>();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: ManifestPath);
            }

            return !Log.HasLoggedErrors;

            static ITaskItem ToTaskItem(StaticWebAssetPackageFile file)
            {
                var result = new TaskItem(file.Id);
                result.SetMetadata(nameof(StaticWebAssetPackageFile.PackagePath), file.PackagePath);
                return result;
            }
        }

        private class StaticWebAssetPackageFile
        {
            public string Id { get; set; }

            public string PackagePath { get; set; }
        }

        private class StaticWebAssetsPackManifest
        {
            public StaticWebAssetPackageFile[] Files { get; set; }

            public string[] ElementsToRemove { get; set; }
        }
    }
}
