// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class PackageManager : IDisposable
    {
        private string _packageLocation = TestUtils.CreateTemporaryFolder("packages");
        private ConcurrentDictionary<string, string> _installedPackages = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string PackTestTemplatesNuGetPackage()
        {
            string dir = Path.GetDirectoryName(typeof(PackageManager).GetTypeInfo().Assembly.Location);
            string projectToPack = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "Microsoft.TemplateEngine.TestTemplates.csproj");
            return PackNuGetPackage(projectToPack);
        }

        public string PackProjectTemplatesNuGetPackage(string templatePackName)
        {
            string dir = Path.GetDirectoryName(typeof(PackageManager).GetTypeInfo().Assembly.Location);
            string projectToPack = Path.Combine(dir, "..", "..", "..", "..", "..", "template_feed", templatePackName, $"{templatePackName}.csproj");
            return PackNuGetPackage(projectToPack);
        }

        public string PackNuGetPackage(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("projectPath cannot be null", nameof(projectPath));
            }
            string absolutePath = Path.GetFullPath(projectPath);
            if (!File.Exists(projectPath))
            {
                throw new ArgumentException($"{projectPath} doesn't exist", nameof(projectPath));
            }
            lock (string.Intern(absolutePath.ToLowerInvariant()))
            {
                if (_installedPackages.TryGetValue(absolutePath, out string packagePath))
                {
                    return packagePath;
                }

                var info = new ProcessStartInfo("dotnet", $"pack {absolutePath} -o {_packageLocation}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                Process p = Process.Start(info);
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new Exception($"Failed to pack the project {projectPath}");
                }

                string createdPackagePath = Directory.GetFiles(_packageLocation).Aggregate(
                    (latest, current) => (latest == null) ? current : File.GetCreationTimeUtc(current) > File.GetCreationTimeUtc(latest) ? current : latest);
                _installedPackages[absolutePath] = createdPackagePath;
                return createdPackagePath;
            }
        }

        public void Dispose()
        {
            Directory.Delete(_packageLocation, true);
        }
    }
}
