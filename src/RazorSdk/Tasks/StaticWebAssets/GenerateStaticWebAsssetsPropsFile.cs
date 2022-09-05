// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateStaticWebAsssetsPropsFile : Task
    {
        private const string SourceType = "SourceType";
        private const string SourceId = "SourceId";
        private const string ContentRoot = "ContentRoot";
        private const string BasePath = "BasePath";
        private const string RelativePath = "RelativePath";
        private const string AssetKind = "AssetKind";
        private const string AssetMode = "AssetMode";
        private const string AssetRole = "AssetRole";
        private const string RelatedAsset = "RelatedAsset";
        private const string AssetTraitName = "AssetTraitName";
        private const string AssetTraitValue = "AssetTraitValue";
        private const string CopyToOutputDirectory = "CopyToOutputDirectory";
        private const string CopyToPublishDirectory = "CopyToPublishDirectory";
        private const string OriginalItemSpec = "OriginalItemSpec";


        [Required]
        public string TargetPropsFilePath { get; set; }

        [Required]
        public ITaskItem[] StaticWebAssets { get; set; }

        public string PackagePathPrefix { get; set; } = "staticwebassets";
        
        public bool AllowEmptySourceType { get; set; }

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
            if (StaticWebAssets.Length == 0)
            {
                return !Log.HasLoggedErrors;
            }

            var itemGroup = new XElement("ItemGroup");
            var orderedAssets = StaticWebAssets.OrderBy(e => e.GetMetadata(BasePath), StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.GetMetadata(RelativePath), StringComparer.OrdinalIgnoreCase);
            foreach (var element in orderedAssets)
            {
                var fullPathExpression = @$"$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\{Normalize(PackagePathPrefix)}\{Normalize(element.GetMetadata(RelativePath))}))";
                itemGroup.Add(new XElement("StaticWebAsset",
                    new XAttribute("Include", fullPathExpression),
                    new XElement(SourceType, "Package"),
                    new XElement(SourceId, element.GetMetadata(SourceId)),
                    new XElement(ContentRoot, @$"$(MSBuildThisFileDirectory)..\{Normalize(PackagePathPrefix)}\"),
                    new XElement(BasePath, element.GetMetadata(BasePath)),
                    new XElement(RelativePath, element.GetMetadata(RelativePath)),
                    new XElement(AssetKind, element.GetMetadata(AssetKind)),
                    new XElement(AssetMode, element.GetMetadata(AssetMode)),
                    new XElement(AssetRole, element.GetMetadata(AssetRole)),
                    new XElement(RelatedAsset, element.GetMetadata(RelatedAsset)),
                    new XElement(AssetTraitName, element.GetMetadata(AssetTraitName)),
                    new XElement(AssetTraitValue, element.GetMetadata(AssetTraitValue)),
                    new XElement(CopyToOutputDirectory, element.GetMetadata(CopyToOutputDirectory)),
                    new XElement(CopyToPublishDirectory, element.GetMetadata(CopyToPublishDirectory)),
                    new XElement(OriginalItemSpec, fullPathExpression)));
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("Project", itemGroup);

            document.Add(root);

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false,
                Async = true
            };

            using var memoryStream = new MemoryStream();
            using (var xmlWriter = XmlWriter.Create(memoryStream, settings))
            {
                document.WriteTo(xmlWriter);
            }

            var data = memoryStream.ToArray();
            WriteFile(data);

            return !Log.HasLoggedErrors;

            static string Normalize(string relativePath) => relativePath.Replace("/", "\\").TrimStart('\\');
        }

        private void WriteFile(byte[] data)
        {
            var dataHash = ComputeHash(data);
            var fileExists = File.Exists(TargetPropsFilePath);
            var existingFileHash = fileExists ? ComputeHash(File.ReadAllBytes(TargetPropsFilePath)) : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating file '{TargetPropsFilePath}' does not exist.");
                File.WriteAllBytes(TargetPropsFilePath, data);
            }
            else if (!string.Equals(dataHash, existingFileHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating '{TargetPropsFilePath}' file because the hash '{dataHash}' is different from existing file hash '{existingFileHash}'.");
                File.WriteAllBytes(TargetPropsFilePath, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping file update because the hash '{dataHash}' has not changed.");
            }
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();

            var result = sha256.ComputeHash(data);
            return Convert.ToBase64String(result);
        }

        private XmlWriter GetXmlWriter(XmlWriterSettings settings)
        {
            var fileStream = new FileStream(TargetPropsFilePath, FileMode.Create);
            return XmlWriter.Create(fileStream, settings);
        }

        private bool ValidateArguments()
        {
            ITaskItem firstAsset = null;

            for (var i = 0; i < StaticWebAssets.Length; i++)
            {
                var webAsset = StaticWebAssets[i];
                if (!EnsureRequiredMetadata(webAsset, SourceId) ||
                    !EnsureRequiredMetadata(webAsset, SourceType, allowEmpty: AllowEmptySourceType) ||
                    !EnsureRequiredMetadata(webAsset, ContentRoot) ||
                    !EnsureRequiredMetadata(webAsset, BasePath) ||
                    !EnsureRequiredMetadata(webAsset, RelativePath))
                {
                    return false;
                }

                if (firstAsset == null)
                {
                    firstAsset = webAsset;
                    continue;
                }

                if (!ValidateMetadataMatches(firstAsset, webAsset, SourceId) ||
                    !ValidateSourceType(webAsset, allowEmpty: AllowEmptySourceType))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ValidateSourceType(ITaskItem candidate, bool allowEmpty)
        {
            var candidateMetadata = candidate.GetMetadata(SourceType);
            if (allowEmpty && string.IsNullOrEmpty(candidateMetadata))
            {
                return true;
            }

            if (!(string.Equals("Discovered", candidateMetadata, StringComparison.Ordinal) || string.Equals("Computed", candidateMetadata, StringComparison.Ordinal)))
            {
                Log.LogError($"Static web asset '{candidate.ItemSpec}' has invalid source type '{candidateMetadata}'.");
                return false;
            }

            return true;
        }

        private bool ValidateMetadataMatches(ITaskItem reference, ITaskItem candidate, string metadata)
        {
            var referenceMetadata = reference.GetMetadata(metadata);
            var candidateMetadata = candidate.GetMetadata(metadata);
            if (!string.Equals(referenceMetadata, candidateMetadata, StringComparison.Ordinal))
            {
                Log.LogError($"Static web assets have different '{metadata}' metadata values '{referenceMetadata}' and '{candidateMetadata}' for '{reference.ItemSpec}' and '{candidate.ItemSpec}'.");
                return false;
            }

            return true;
        }

        private bool EnsureRequiredMetadata(ITaskItem item, string metadataName, bool allowEmpty = false)
        {
            var value = item.GetMetadata(metadataName);
            var isInvalidValue = allowEmpty ? !HasMetadata(item, metadataName) : string.IsNullOrEmpty(value);

            if (isInvalidValue)
            {
                Log.LogError($"Missing required metadata '{metadataName}' for '{item.ItemSpec}'.");
                return false;
            }

            return true;
        }

        private bool HasMetadata(ITaskItem item, string metadataName)
        {
            foreach (var name in item.MetadataNames)
            {
                if (string.Equals(metadataName, (string)name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
