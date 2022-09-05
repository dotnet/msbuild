// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class StaticWebAssetsGeneratePackManifest : Task
    {
        // Since the manifest is only used at build time, it's ok for it to use the relaxed
        // json escaping (which is also what MVC uses by default) and to produce indented output
        // since that makes it easier to inspect the manifest when necessary.
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public ITaskItem[] AdditionalPackageFiles { get; set; }

        public ITaskItem[] AdditionalElementsToRemoveFromPacking { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string ManifestPath { get; set; }

        public override bool Execute()
        {
            if (Assets.Length == 0)
            {
                // Do nothing if there are no assets to pack.
                Log.LogMessage(MessageImportance.Low, "Skipping manifest creation because there are no static web assets to pack.");
                return true;
            }

            var packageFiles = new List<StaticWebAssetPackageFile>();

            foreach (var file in AdditionalPackageFiles)
            {
                packageFiles.Add(new StaticWebAssetPackageFile
                {
                    Id = file.ItemSpec,
                    PackagePath = file.GetMetadata("PackagePath")
                });
            }

            foreach (var asset in Assets)
            {
                packageFiles.Add(new StaticWebAssetPackageFile
                {
                    Id = asset.ItemSpec,
                    PackagePath = asset.GetMetadata("TargetPath")
                });
            }

            packageFiles.Sort((x,y) => string.Compare(x.Id, y.Id, StringComparison.Ordinal));

            var manifest = new StaticWebAssetsPackManifest
            {
                Files = packageFiles.ToArray(),
                ElementsToRemove = AdditionalElementsToRemoveFromPacking.Select(e => e.ItemSpec).OrderBy(id => id).ToArray()
            };

            PersistManifest(manifest);

            return !Log.HasLoggedErrors;
        }

        private void PersistManifest(StaticWebAssetsPackManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestSerializationOptions);
            var dataHash = ComputeHash(data);
            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ? ComputeHash(File.ReadAllBytes(ManifestPath)) : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest because manifest file '{ManifestPath}' does not exist.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!string.Equals(dataHash, existingManifestHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest version '{dataHash}' is different from existing manifest hash '{existingManifestHash}'.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping manifest update because manifest version '{dataHash}' has not changed.");
            }
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();

            var result = sha256.ComputeHash(data);
            return Convert.ToBase64String(result);
        }

        private class StaticWebAssetPackageFile
        {
            public string Id { get; set; }

            public string PackagePath { get; set; }
        }

        private class StaticWebAssetsPackManifest
        {
            public StaticWebAssetPackageFile[] Files { get; set; }

            public string [] ElementsToRemove { get; set; }
        }
    }
}
