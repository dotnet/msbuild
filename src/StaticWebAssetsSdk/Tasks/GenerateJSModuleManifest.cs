// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class GenerateJSModuleManifest : Task
    {
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        [Required]
        public string OutputFile { get; set; }

        [Required]
        public ITaskItem[] JsModules { get; set; }

        public override bool Execute()
        {
            var modules = JsModules.Select(StaticWebAsset.FromTaskItem).Select(s => s.ComputeTargetPath("", '/')).ToArray();
            Array.Sort(modules, StringComparer.Ordinal);

            PersistModules(modules);
            return !Log.HasLoggedErrors;
        }

        private void PersistModules(string[] modules)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(modules, ManifestSerializationOptions);
            using var sha256 = SHA256.Create();
            var currentHash = sha256.ComputeHash(data);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));

            var fileExists = File.Exists(OutputFile);
            var existingManifestHash = fileExists ? sha256.ComputeHash(File.ReadAllBytes(OutputFile)) : Array.Empty<byte>();

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest because manifest file '{OutputFile}' does not exist.");
                File.WriteAllBytes(OutputFile, data);
            }
            else if (!currentHash.SequenceEqual(existingManifestHash))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest version '{Convert.ToBase64String(currentHash)}' is different from existing manifest hash '{Convert.ToBase64String(existingManifestHash)}'.");
                File.WriteAllBytes(OutputFile, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest version '{Convert.ToBase64String(currentHash)}' has not changed.");
            }
        }
    }
}
