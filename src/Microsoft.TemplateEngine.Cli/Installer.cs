// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        public void AddInstallDescriptorForLocation(Guid mountPointId, bool isPartOfAnOptionalWorkload, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.TryAddDescriptorForLocation(mountPointId, isPartOfAnOptionalWorkload, out descriptorList);
        }

        public void InstallPackages(IEnumerable<string> installationRequests)
            => InstallPackages(installationRequests, null, false, false);

        public void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources)
            => InstallPackages(installationRequests, nuGetSources, false, false);

        public void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources, bool debugAllowDevInstall)
            => InstallPackages(installationRequests, nuGetSources, debugAllowDevInstall, false);

        public void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources, bool debugAllowDevInstall, bool interactive)
            => InstallPackages(installationRequests.Select(x => new InstallationRequest(x)), nuGetSources, debugAllowDevInstall, interactive);

        public void InstallPackages(IEnumerable<InstallationRequest> installationRequests, IList<string> nuGetSources = null, bool debugAllowDevInstall = false, bool interactive = false)
        {
            List<InstallationRequest> localSources = new List<InstallationRequest>();
            List<Package> packages = new List<Package>();
            List<Package> packagesFromOptionalWorkloads = new List<Package>();

            foreach (InstallationRequest request in installationRequests)
            {
                string req = request.InstallString;

                //If the request string doesn't have any wild cards or probable path indicators,
                //  and doesn't have a "::" delimiter either, try to convert it to "latest package"
                //  form
                if (OriginalRequestIsImplicitPackageVersionSyntax(request.InstallString))
                {
                    req += "::*";
                }

                if (Package.TryParse(req, out Package package))
                {
                    if (request.IsPartOfAnOptionalWorkload)
                    {
                        packagesFromOptionalWorkloads.Add(package);
                    }
                    else
                    {
                        packages.Add(package);
                    }
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

            if (packages.Count + packagesFromOptionalWorkloads.Count > 0)
            {
                IList<string> additionalDotNetArguments = interactive ? new string[] { "--interactive" } : null;

                if (packages.Count > 0)
                {
                    InstallRemotePackages(packages, nuGetSources, additionalDotNetArguments, false);
                }

                if (packagesFromOptionalWorkloads.Count > 0)
                {
                    InstallRemotePackages(packagesFromOptionalWorkloads, nuGetSources, additionalDotNetArguments, true);
                }
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

        private void UninstallOtherVersionsOfSamePackage(IInstallUnitDescriptor descriptor)
        {
            IReadOnlyList<IInstallUnitDescriptor> allDescriptors = ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.Descriptors.Values.ToList();

            foreach (IInstallUnitDescriptor testDescriptor in allDescriptors)
            {
                if (string.Equals(descriptor.Identifier, testDescriptor.Identifier, StringComparison.OrdinalIgnoreCase)
                    && descriptor.FactoryId == testDescriptor.FactoryId
                    && descriptor.DescriptorId != testDescriptor.DescriptorId)
                {
                    if (descriptor.MountPointId != testDescriptor.MountPointId)
                    {
                        // Uninstalls the mount point and the descriptor(s) for packs that were installed under that mount point.
                        UninstallMountPoint(testDescriptor.MountPointId);
                    }
                    else
                    {
                        // The new install is in the same place as the old install. Don't remove the mount point, just the old descriptor.
                        // This is for when the exact same pack is installed over-the-top of an existing install of it.
                        // Works for both zip/nupkg and for local file sources.
                        ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.RemoveDescriptor(testDescriptor);
                    }
                }
            }
        }

        public IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequestList)
        {
            List<string> uninstallFailures = new List<string>();

            Dictionary<string, IInstallUnitDescriptor> descriptorsById = ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.Descriptors
                                                                                .ToDictionary(x => x.Value.Identifier, x => x.Value);

            foreach (string uninstallRequest in uninstallRequestList)
            {
                // TODO - possible match on other than the exact identifier
                if (descriptorsById.TryGetValue(uninstallRequest, out IInstallUnitDescriptor installDescriptor))
                {
                    UninstallMountPoint(installDescriptor.MountPointId);
                }
                else
                {
                    uninstallFailures.Add(uninstallRequest);
                }
            }

            return uninstallFailures;
        }

        private bool UninstallMountPoint(Guid mountPointId)
        {
            if (!_environmentSettings.SettingsLoader.TryGetMountPointInfo(mountPointId, out MountPointInfo mountPoint))
            {
                return false;
            }

            IReadOnlyCollection<Guid> mountPointFamily = GetDescendantMountPointsFromParent(mountPointId);

            //Find all of the things that refer to any of the mount points we've got
            _environmentSettings.SettingsLoader.RemoveMountPoints(mountPointFamily);
            ((SettingsLoader)(_environmentSettings.SettingsLoader)).InstallUnitDescriptorCache.RemoveDescriptorsForLocationList(mountPointFamily);
            _environmentSettings.SettingsLoader.Save();

            if (mountPoint.Place.StartsWith(_paths.User.Packages, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _environmentSettings.Host.FileSystem.FileDelete(mountPoint.Place);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private IReadOnlyCollection<Guid> GetDescendantMountPointsFromParent(Guid parent)
        {
            HashSet<Guid> mountPoints = new HashSet<Guid> { parent };
            bool anyFound = true;

            while (anyFound)
            {
                anyFound = false;

                foreach (MountPointInfo possibleChild in _environmentSettings.SettingsLoader.MountPoints)
                {
                    if (mountPoints.Contains(possibleChild.ParentMountPointId))
                    {
                        anyFound |= mountPoints.Add(possibleChild.MountPointId);
                    }
                }
            }

            return mountPoints;
        }

        /// <summary>
        /// Acquires given nuget packages with 'dotnet restore' and installs them.
        /// </summary>
        /// <param name="packages">Packages to be installed.</param>
        /// <param name="nuGetSources">Nuget sources to look for the packages.</param>
        /// <param name="additionalDotNetArguments">Additional arguments to be passed to 'dotnet restore'.</param>
        /// <param name="isPartOfAnOptionalWorkload">Specifies if given packages are part of an optional workload. This should be correct for
        /// each of the specified packages (do not mix optional and non-optional packages when calling this method).</param>
        private void InstallRemotePackages(List<Package> packages, IList<string> nuGetSources, IList<string> additionalDotNetArguments, bool isPartOfAnOptionalWorkload)
        {
            const string packageRef = @"    <PackageReference Include=""{0}"" Version=""{1}"" />";
            const string projectFile = @"<Project ToolsVersion=""15.0"" Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp3.1</TargetFrameworks>
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

            List<string> restoreArgsList = new List<string>();
            restoreArgsList.Add(proj);
            restoreArgsList.Add("--packages");
            restoreArgsList.Add(restored);

            if (nuGetSources != null)
            {
                foreach (string nuGetSource in nuGetSources)
                {
                    restoreArgsList.Add("--source");
                    restoreArgsList.Add(nuGetSource);
                }
            }

            if (additionalDotNetArguments != null)
            {
                restoreArgsList.AddRange(additionalDotNetArguments);
            }

            var restoreArgs = restoreArgsList.ToArray();

            Dotnet.Restore(restoreArgs).ForwardStdOut().ForwardStdErr().Execute();
            string stagingDir = Path.Combine(_paths.User.ScratchDir, "Staging");
            _paths.CreateDirectory(stagingDir);

            List<InstallationRequest> newLocalPackages = new List<InstallationRequest>();
            foreach (string packagePath in _paths.EnumerateFiles(restored, "*.nupkg", SearchOption.AllDirectories))
            {
                string stagingPathForPackage = Path.Combine(stagingDir, Path.GetFileName(packagePath));
                _paths.Copy(packagePath, stagingPathForPackage);
                newLocalPackages.Add(new InstallationRequest(stagingPathForPackage, isPartOfAnOptionalWorkload));
            }

            InstallLocalPackages(newLocalPackages, false);
            _paths.DeleteDirectory(_paths.User.ScratchDir);
        }

        private void InstallLocalPackages(IEnumerable<InstallationRequest> localInstallationRequests, bool debugAllowDevInstall)
        {
            foreach (InstallationRequest localInstallRequest in localInstallationRequests)
            {
                if (string.IsNullOrWhiteSpace(localInstallRequest.InstallString))
                {
                    continue;
                }

                string req = localInstallRequest.InstallString.Trim();
                req = _environmentSettings.Environment.ExpandEnvironmentVariables(req);
                string pattern = null;

                int wildcardIndex = req.IndexOfAny(new[] { '*', '?' });

                if (wildcardIndex > -1)
                {
                    int lastSlashBeforeWildcard = req.LastIndexOfAny(new[] { '\\', '/' });
                    if (lastSlashBeforeWildcard >= 0)
                    {
                        pattern = req.Substring(lastSlashBeforeWildcard + 1);
                        req = req.Substring(0, lastSlashBeforeWildcard);
                    }
                }

                try
                {
                    string installString = null;

                    if (pattern != null)
                    {
                        string fullDirectory = new DirectoryInfo(req).FullName;
                        installString = Path.Combine(fullDirectory, pattern);
                    }
                    else if (_environmentSettings.Host.FileSystem.DirectoryExists(req) || _environmentSettings.Host.FileSystem.FileExists(req))
                    {
                        installString = new DirectoryInfo(req).FullName;
                    }
                    else
                    {
                        _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.CouldNotFindItemToInstall, req), null, 0);
                    }

                    if (installString != null)
                    {
                        ((SettingsLoader)(_environmentSettings.SettingsLoader)).UserTemplateCache.Scan(installString, out IReadOnlyList<Guid> contentMountPointIds, debugAllowDevInstall);

                        foreach (Guid mountPointId in contentMountPointIds)
                        {
                            AddInstallDescriptorForLocation(mountPointId, localInstallRequest.IsPartOfAnOptionalWorkload, out IReadOnlyList<IInstallUnitDescriptor> descriptorList);

                            foreach (IInstallUnitDescriptor descriptor in descriptorList)
                            {
                                UninstallOtherVersionsOfSamePackage(descriptor);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format(LocalizableStrings.BadPackageSpec, req), null, 0);

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
