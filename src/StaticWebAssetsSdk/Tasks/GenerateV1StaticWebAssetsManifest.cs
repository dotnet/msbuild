// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Xml;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class GenerateV1StaticWebAssetsManifest : Task
    {
        private const string ContentRoot = "ContentRoot";
        private const string BasePath = "BasePath";
        private const string NodePath = "Path";
        private const string SourceId = "SourceId";

        [Required]
        public string TargetManifestPath { get; set; }

        [Required]
        public ITaskItem[] ContentRootDefinitions { get; set; }

        public override bool Execute()
        {
            if (!ValidateArguments())
            {
                return false;
            }

            return ExecuteCore();
        }

        private bool ExecuteCore()
        {
            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement(
                "StaticWebAssets",
                new XAttribute("Version", "1.0"),
                CreateNodes());

            document.Add(root);

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false,
                Async = true
            };

            PersistManifest(document, settings);

            return !Log.HasLoggedErrors;
        }

        private void PersistManifest(XDocument document, XmlWriterSettings settings)
        {
            using var memory = new MemoryStream();
            using var xmlWriter = XmlWriter.Create(memory, settings);
            document.WriteTo(xmlWriter);
            xmlWriter.Flush();
            memory.Seek(0, SeekOrigin.Begin);
            var data = memory.ToArray();

            using var sha256 = SHA256.Create();
            var currentHash = sha256.ComputeHash(data);

            var fileExists = File.Exists(TargetManifestPath);
            var existingManifestHash = fileExists ? sha256.ComputeHash(File.ReadAllBytes(TargetManifestPath)) : Array.Empty<byte>();

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest because manifest file '{TargetManifestPath}' does not exist.");
                File.WriteAllBytes(TargetManifestPath, data);
            }
            else if (!currentHash.SequenceEqual(existingManifestHash))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest version '{Convert.ToBase64String(currentHash)}' is different from existing manifest hash '{Convert.ToBase64String(existingManifestHash)}'.");
                File.WriteAllBytes(TargetManifestPath, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest version '{Convert.ToBase64String(currentHash)}' has not changed.");
            }
        }

        private IEnumerable<XElement> CreateNodes()
        {
            var nodes = new List<XElement>();
            for (var i = 0; i < ContentRootDefinitions.Length; i++)
            {
                var contentRootDefinition = ContentRootDefinitions[i];
                var basePath = contentRootDefinition.GetMetadata(BasePath);
                var contentRoot = contentRootDefinition.GetMetadata(ContentRoot);

                // basePath is meant to be a prefix for the files under contentRoot. MSbuild
                // normalizes '\' according to the OS, but this is going to be part of the url
                // so it needs to always be '/'.
                var normalizedBasePath = basePath.Replace("\\", "/");

                // contentRoot can have forward and trailing slashes and sometimes consecutive directory
                // separators. To be more flexible we will normalize the content root so that it contains a
                // single trailing separator.
                var normalizedContentRoot = $"{contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}{Path.DirectorySeparatorChar}";

                // Here we simply skip additional items that have the same base path and same content root.
                if (!nodes.Exists(e => e.Attribute("BasePath").Value.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase) &&
                    e.Attribute("Path").Value.Equals(normalizedContentRoot, StringComparison.OrdinalIgnoreCase)))
                {
                    nodes.Add(new XElement("ContentRoot",
                        new XAttribute("BasePath", normalizedBasePath),
                        new XAttribute("Path", normalizedContentRoot)));
                }
            }

            // Its important that we order the nodes here to produce a manifest deterministically.
            return nodes.OrderBy(e=>e.Attribute(BasePath).Value).ThenBy(e => e.Attribute(NodePath).Value);
        }

        private bool ValidateArguments()
        {
            for (var i = 0; i < ContentRootDefinitions.Length; i++)
            {
                var contentRootDefinition = ContentRootDefinitions[i];
                if (!EnsureRequiredMetadata(contentRootDefinition, BasePath) ||
                    !EnsureRequiredMetadata(contentRootDefinition, ContentRoot) ||
                    !EnsureRequiredMetadata(contentRootDefinition, SourceId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool EnsureRequiredMetadata(ITaskItem item, string metadataName)
        {
            var value = item.GetMetadata(metadataName);
            if (string.IsNullOrEmpty(value))
            {
                Log.LogError($"Missing required metadata '{metadataName}' for '{item.ItemSpec}'.");
                return false;
            }

            return true;
        }
    }
}
