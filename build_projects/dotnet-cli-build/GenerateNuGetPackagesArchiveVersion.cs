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
        public string SDKVersion { get; set; }

        [Required]
        public string ToolPath { get; set; }

        [Output]
        public String Version { get; set; }

        private static string[][] _templatesAndArgs = new string[][]
        {
            new string[] { "mvc", "-au individual" },
        };

        public override bool Execute()
        {
            var dataToHash = string.Empty;

            foreach (var newArgs in _templatesAndArgs)
            {
                var targetDir = Path.GetTempFileName();
                File.Delete(targetDir);
                Directory.CreateDirectory(targetDir);
                var outputDir = Path.Combine(targetDir, newArgs[0]);
                Directory.CreateDirectory(outputDir);
                var newTask = new DotNetNew
                {
                    ToolPath = ToolPath,
                    TemplateType = newArgs[0],
                    TemplateArgs = newArgs[1] + $" --debug:ephemeral-hive -n TempProject -o \"{outputDir}\" --no-restore",
                    HostObject = HostObject,
                    BuildEngine = BuildEngine
                };
                newTask.Execute();
                var templatePath = Path.Combine(outputDir, "TempProject.csproj");

                var rootElement = ProjectRootElement.Open(templatePath);
                var packageRefs = rootElement.Items.Where(i => i.ItemType == "PackageReference").ToList();

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

                var frameworkVersion = rootElement.Properties.LastOrDefault(p => p.Name == "RuntimeFrameworkVersion");
                if (frameworkVersion != null)
                {
                    dataToHash += $"RuntimeFrameworkVersion={frameworkVersion.Value}";
                }

                Directory.Delete(targetDir, true);
            }

            dataToHash += SDKVersion;

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
