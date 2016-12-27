// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateNuGetPackagesArchiveVersion : Task
    {
        public GenerateNuGetPackagesArchiveVersion()
        {
        }

        [Required]
        public string RepoRoot { get; set; }

        [Output]
        public String Version { get; set; }

        public override bool Execute()
        {
            var webTemplatePath = Path.Combine(
                RepoRoot,
                "src",
                "dotnet",
                "commands",
                "dotnet-new",
                "CSharp_Web",
                "$projectName$.csproj");

            var rootElement = ProjectRootElement.Open(webTemplatePath);
            var packageRefs = rootElement.Items.Where(i => i.ItemType == "PackageReference").ToList();

            var dataToHash = string.Empty;
            foreach (var packageRef in packageRefs)
            {
                dataToHash += $"{packageRef.Include},";
                if (packageRef.HasMetadata)
                {
                    foreach (var metadata in packageRef.Metadata)
                    {
                        dataToHash += $"{metadata.Name}={metadata.Value};";
                    }
                }
            }

            Log.LogMessage($"NuGet Packages Archive Data To Hash: '{dataToHash}'");

            var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.Unicode.GetBytes(dataToHash));
            Version = GetHashString(hashBytes);

            Log.LogMessage($"NuGet Packages Archive Version: '{Version}'");

            return true;
        }

        private string GetHashString(byte[] hashBytes)
        {
            StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
            foreach (var b in hashBytes)
            {
                builder.AppendFormat("{0:x2}", b);
            }
            return builder.ToString();
        }
    }
}
