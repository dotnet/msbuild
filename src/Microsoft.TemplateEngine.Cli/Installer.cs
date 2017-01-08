// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    internal class Installer : IInstaller
    {
        public void InstallPackages(IEnumerable<string> installationRequests)
        {
            List<string> localSources = new List<string>();
            List<Package> packages = new List<Package>();

            foreach (string request in installationRequests)
            {
                if (Package.TryParse(request, out Package package))
                {
                    packages.Add(package);
                }
                else
                {
                    localSources.Add(request);
                }
            }

            if (localSources.Count > 0)
            {
                InstallLocalPackages(localSources);
            }

            if (packages.Count > 0)
            {
                InstallRemotePackages(packages);
            }
        }

        private void InstallRemotePackages(List<Package> packages)
        {
            const string packageRef = @"    <PackageReference Include=""{0}"" Version=""{1}"" />";
            const string projectFile = @"<Project ToolsVersion=""15.0"" Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp1.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
{0}
  </ItemGroup>
</Project>";

            Paths.User.ScratchDir.CreateDirectory();
            string proj = Path.Combine(Paths.User.ScratchDir, "restore.csproj");
            StringBuilder references = new StringBuilder();

            foreach (Package pkg in packages)
            {
                references.AppendLine(string.Format(packageRef, pkg.Name, pkg.Version));
            }

            string content = string.Format(projectFile, references.ToString());
            proj.WriteAllText(content);

            Paths.User.Packages.CreateDirectory();
            string restored = Path.Combine(Paths.User.ScratchDir, "Packages");
            CommandResult commandResult = Command.CreateDotNet("restore", new[] { proj, "--packages", restored }).ForwardStdErr().Execute();

            List<string> newLocalPackages = new List<string>();
            foreach (string packagePath in restored.EnumerateFiles("*.nupkg", SearchOption.AllDirectories))
            {
                string path = Path.Combine(Paths.User.Packages, Path.GetFileName(packagePath));
                packagePath.Copy(path);
                newLocalPackages.Add(path);
            }

            Paths.User.ScratchDir.DeleteDirectory();
            InstallLocalPackages(newLocalPackages);
        }

        private void InstallLocalPackages(IReadOnlyList<string> packageNames)
        {
            List<string> toInstall = new List<string>();

            foreach (string package in packageNames)
            {
                string pkg = package.Trim();
                pkg = EngineEnvironmentSettings.Environment.ExpandEnvironmentVariables(pkg);
                string pattern = null;

                int wildcardIndex = pkg.IndexOfAny(new[] { '*', '?' });

                if (wildcardIndex > -1)
                {
                    int lastSlashBeforeWildcard = pkg.LastIndexOfAny(new[] { '\\', '/' });
                    pattern = pkg.Substring(lastSlashBeforeWildcard + 1);
                    pkg = pkg.Substring(0, lastSlashBeforeWildcard);
                }

                try
                {
                    if (pattern != null)
                    {
                        string fullDirectory = new DirectoryInfo(pkg).FullName;
                        string fullPathGlob = Path.Combine(fullDirectory, pattern);
                        TemplateCache.Scan(fullPathGlob);
                    }
                    else if (EngineEnvironmentSettings.Host.FileSystem.DirectoryExists(pkg) || EngineEnvironmentSettings.Host.FileSystem.FileExists(pkg))
                    {
                        string packageLocation = new DirectoryInfo(pkg).FullName;
                        TemplateCache.Scan(packageLocation);
                    }
                    else
                    {
                        EngineEnvironmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.BadPackageSpec, pkg), null, 0);
                    }
                }
                catch
                {
                    EngineEnvironmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.BadPackageSpec, pkg), null, 0);
                }
            }

            TemplateCache.WriteTemplateCaches();
        }
    }
}
