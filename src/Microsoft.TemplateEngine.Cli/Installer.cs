// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    internal class Installer : IInstaller
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public Installer(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        public void AddInstallDescriptorForLocation(Guid mountPointId)
        {
            ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.TryAddDescriptorForLocation(mountPointId);
        }

        public void InstallPackages(IEnumerable<string> installationRequests) => InstallPackages(installationRequests, null, false);

        public void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources) => InstallPackages(installationRequests, nuGetSources, false);

        public void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources, bool debugAllowDevInstall)
        {
            List<string> localSources = new List<string>();
            List<Package> packages = new List<Package>();

            foreach (string request in installationRequests)
            {
                string req = request;

                //If the request string doesn't have any wild cards or probable path indicators,
                //  and doesn't have a "::" delimiter either, try to convert it to "latest package"
                //  form
                if (OriginalRequestIsImplicitPackageVersionSyntax(request))
                {
                    req += "::*";
                }

                if (Package.TryParse(req, out Package package))
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
                InstallLocalPackages(localSources, debugAllowDevInstall);
            }

            if (packages.Count > 0)
            {
                InstallRemotePackages(packages, nuGetSources);
            }

            _environmentSettings.SettingsLoader.Save();
        }

        private bool OriginalRequestIsImplicitPackageVersionSyntax(string req)
        {
            if (req.IndexOfAny(new[] { '*', '?', '/', '\\' }) < 0 && req.IndexOf("::", StringComparison.Ordinal) < 0)
            {
                bool localFileSystemEntryExists = false;

                try
                {
                    localFileSystemEntryExists = _environmentSettings.Host.FileSystem.FileExists(req)
                                                 || _environmentSettings.Host.FileSystem.DirectoryExists(req);
                }
                catch
                {
                }

                return !localFileSystemEntryExists;
            }

            return false;
        }

        public IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequests)
        {
            List<string> uninstallFailures = new List<string>();
            foreach (string uninstall in uninstallRequests)
            {
                string prefix = Path.Combine(_paths.User.Packages, uninstall);
                IReadOnlyList<MountPointInfo> rootMountPoints = _environmentSettings.SettingsLoader.MountPoints.Where(x =>
                {
                    if (x.ParentMountPointId != Guid.Empty)
                    {
                        return false;
                    }

                    if (uninstall.IndexOfAny(new[] { '/', '\\' }) < 0)
                    {
                        if (x.Place.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) && x.Place.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    if (string.Equals(x.Place, uninstall, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    else if (x.Place.Length > uninstall.Length)
                    {
                        string place = x.Place.Replace('\\', '/');
                        string match = uninstall.Replace('\\', '/');

                        if (match[match.Length - 1] != '/')
                        {
                            match += "/";
                        }

                        return place.StartsWith(match, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
                }).ToList();

                if (rootMountPoints.Count == 0)
                {
                    uninstallFailures.Add(uninstall);
                    continue;
                }

                HashSet<Guid> mountPoints = new HashSet<Guid>(rootMountPoints.Select(x => x.MountPointId));
                bool isSearchComplete = false;
                while (!isSearchComplete)
                {
                    isSearchComplete = true;
                    foreach (MountPointInfo possibleChild in _environmentSettings.SettingsLoader.MountPoints)
                    {
                        if (mountPoints.Contains(possibleChild.ParentMountPointId))
                        {
                            isSearchComplete &= !mountPoints.Add(possibleChild.MountPointId);
                        }
                    }
                }

                //Find all of the things that refer to any of the mount points we've got
                _environmentSettings.SettingsLoader.RemoveMountPoints(mountPoints);
                ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.RemoveDescriptorsForLocationList(mountPoints);
                _environmentSettings.SettingsLoader.Save();

                foreach (MountPointInfo mountPoint in rootMountPoints)
                {
                    if (mountPoint.Place.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            _environmentSettings.Host.FileSystem.FileDelete(mountPoint.Place);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return uninstallFailures;
        }

        private void InstallRemotePackages(List<Package> packages, IList<string> nuGetSources)
        {
            const string packageRef = @"    <PackageReference Include=""{0}"" Version=""{1}"" />";
            const string projectFile = @"<Project ToolsVersion=""15.0"" Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp1.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Remove=""Microsoft.NETCore.App"" />
{0}
  </ItemGroup>
</Project>";

            _paths.CreateDirectory(_paths.User.ScratchDir);
            string proj = Path.Combine(_paths.User.ScratchDir, "restore.csproj");
            StringBuilder references = new StringBuilder();

            foreach (Package pkg in packages)
            {
                references.AppendLine(string.Format(packageRef, pkg.Name, pkg.Version));
            }

            string content = string.Format(projectFile, references.ToString());
            _paths.WriteAllText(proj, content);

            string restored = Path.Combine(_paths.User.ScratchDir, "Packages");

            int additionalSlots = nuGetSources?.Count * 2 ?? 0;

            string[] restoreArgs = new string[3 + additionalSlots];
            restoreArgs[0] = proj;
            restoreArgs[1] = "--packages";
            restoreArgs[2] = restored;

            if (nuGetSources != null)
            {
                for (int i = 0; i < nuGetSources.Count; ++i)
                {
                    restoreArgs[3 + 2 * i] = "--source";
                    restoreArgs[4 + 2 * i] = nuGetSources[i];
                }
            }

            Dotnet.Restore(restoreArgs).ForwardStdOut().ForwardStdErr().Execute();
            string stagingDir = Path.Combine(_paths.User.ScratchDir, "Staging");
            _paths.CreateDirectory(stagingDir);

            List<string> newLocalPackages = new List<string>();
            foreach (string packagePath in _paths.EnumerateFiles(restored, "*.nupkg", SearchOption.AllDirectories))
            {
                string stagingPathForPackage = Path.Combine(stagingDir, Path.GetFileName(packagePath));
                _paths.Copy(packagePath, stagingPathForPackage);
                newLocalPackages.Add(stagingPathForPackage);
            }

            InstallLocalPackages(newLocalPackages, false);
            _paths.DeleteDirectory(_paths.User.ScratchDir);
        }

        private void InstallLocalPackages(IReadOnlyList<string> packageNames, bool debugAllowDevInstall)
        {
            List<string> toInstall = new List<string>();

            foreach (string package in packageNames)
            {
                if (package == null)
                {
                    continue;
                }

                string pkg = package.Trim();
                pkg = _environmentSettings.Environment.ExpandEnvironmentVariables(pkg);
                string pattern = null;

                int wildcardIndex = pkg.IndexOfAny(new[] { '*', '?' });

                if (wildcardIndex > -1)
                {
                    int lastSlashBeforeWildcard = pkg.LastIndexOfAny(new[] { '\\', '/' });
                    if (lastSlashBeforeWildcard >= 0)
                    {
                        pattern = pkg.Substring(lastSlashBeforeWildcard + 1);
                        pkg = pkg.Substring(0, lastSlashBeforeWildcard);
                    }
                }

                try
                {
                    if (pattern != null)
                    {
                        string fullDirectory = new DirectoryInfo(pkg).FullName;
                        string fullPathGlob = Path.Combine(fullDirectory, pattern);
                        ((SettingsLoader)(_environmentSettings.SettingsLoader)).UserTemplateCache.Scan(fullPathGlob, out IReadOnlyList<Guid> contentMountPointIds, debugAllowDevInstall);

                        foreach (Guid mountPointId in contentMountPointIds)
                        {
                            AddInstallDescriptorForLocation(mountPointId);
                        }
                    }
                    else if (_environmentSettings.Host.FileSystem.DirectoryExists(pkg) || _environmentSettings.Host.FileSystem.FileExists(pkg))
                    {
                        string packageLocation = new DirectoryInfo(pkg).FullName;
                        ((SettingsLoader)(_environmentSettings.SettingsLoader)).UserTemplateCache.Scan(packageLocation, out IReadOnlyList<Guid> contentMountPointIds, debugAllowDevInstall);

                        foreach (Guid mountPointId in contentMountPointIds)
                        {
                            AddInstallDescriptorForLocation(mountPointId);
                        }
                    }
                    else
                    {
                        _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.CouldNotFindItemToInstall, pkg), null, 0);
                    }
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.BadPackageSpec, pkg), null, 0);

                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_NEW_DEBUG")))
                    {
                        _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecificationDetails", ex.ToString(), null, 0);
                    }
                    else
                    {
                        _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecificationDetails", ex.Message, null, 0);
                    }
                }
            }
        }
    }
}
