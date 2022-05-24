// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class StaticWebAssetsGeneratePackagePropsFile : Task
    {
        [Required]
        public string PropsFileImport { get; set; }

        [Required]
        public string BuildTargetPath { get; set; }

        public override bool Execute()
        {
            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement(
                "Project",
                new XElement("Import",
                    new XAttribute("Project", PropsFileImport)));

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
        }

        private void WriteFile(byte[] data)
        {
            var dataHash = ComputeHash(data);
            var fileExists = File.Exists(BuildTargetPath);
            var existingFileHash = fileExists ? ComputeHash(File.ReadAllBytes(BuildTargetPath)) : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating file '{BuildTargetPath}' does not exist.");
                File.WriteAllBytes(BuildTargetPath, data);
            }
            else if (!string.Equals(dataHash, existingFileHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating '{BuildTargetPath}' file because the hash '{dataHash}' is different from existing file hash '{existingFileHash}'.");
                File.WriteAllBytes(BuildTargetPath, data);
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

    }
}
