// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageInstaller : IToolPackageInstaller
    {
        private readonly IToolPackageStore _store;
        private readonly IProjectRestorer _projectRestorer;
        private readonly FilePath? _tempProject;
        private readonly DirectoryPath _offlineFeed;

        public ToolPackageInstaller(
            IToolPackageStore store,
            IProjectRestorer projectRestorer,
            FilePath? tempProject = null,
            DirectoryPath? offlineFeed = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _projectRestorer = projectRestorer ?? throw new ArgumentNullException(nameof(projectRestorer));
            _tempProject = tempProject;
            _offlineFeed = offlineFeed ?? new DirectoryPath(CliFolderPathCalculator.CliFallbackFolderPath);
        }

        public IToolPackage InstallPackage(
            PackageLocation packageLocation,
            PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null)
        {
            var packageRootDirectory = _store.GetRootPackageDirectory(packageId);
            string rollbackDirectory = null;

            return TransactionalAction.Run<IToolPackage>(
                action: () =>
                {
                    try
                    {
                        var stageDirectory = _store.GetRandomStagingDirectory();
                        Directory.CreateDirectory(stageDirectory.Value);
                        rollbackDirectory = stageDirectory.Value;

                        string tempProject = CreateDirectoryWithTempProject(
                            packageId: packageId,
                            versionRange: versionRange,
                            targetFramework: string.IsNullOrEmpty(targetFramework) ? BundledTargetFramework.GetTargetFrameworkMoniker() : targetFramework,
                            restoreDirectory: stageDirectory,
                            assetJsonOutputDirectory: stageDirectory,
                            rootConfigDirectory: packageLocation.RootConfigDirectory,
                            additionalFeeds: packageLocation.AdditionalFeeds);

                        try
                        {
                            _projectRestorer.Restore(
                                new FilePath(tempProject),
                                packageLocation,
                                verbosity: verbosity);
                        }
                        finally
                        {
                            File.Delete(tempProject);
                        }

                        var version = _store.GetStagedPackageVersion(stageDirectory, packageId);
                        var packageDirectory = _store.GetPackageDirectory(packageId, version);
                        if (Directory.Exists(packageDirectory.Value))
                        {
                            throw new ToolPackageException(
                                string.Format(
                                    CommonLocalizableStrings.ToolPackageConflictPackageId,
                                    packageId,
                                    version.ToNormalizedString()));
                        }

                        Directory.CreateDirectory(packageRootDirectory.Value);
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(stageDirectory.Value, packageDirectory.Value));
                        rollbackDirectory = packageDirectory.Value;

                        return new ToolPackageInstance(id: packageId,
                            version: version,
                            packageDirectory: packageDirectory,
                            assetsJsonParentDirectory: packageDirectory);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CommonLocalizableStrings.FailedToInstallToolPackage,
                                packageId,
                                ex.Message),
                            ex);
                    }
                },
                rollback: () =>
                {
                    if (!string.IsNullOrEmpty(rollbackDirectory) && Directory.Exists(rollbackDirectory))
                    {
                        Directory.Delete(rollbackDirectory, true);
                    }

                    // Delete the root if it is empty
                    if (Directory.Exists(packageRootDirectory.Value) &&
                        !Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                    {
                        Directory.Delete(packageRootDirectory.Value, false);
                    }
                });
        }

        public IToolPackage InstallPackageToExternalManagedLocation(
            PackageLocation packageLocation,
            PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null)
        {
            var tempDirectoryForAssetJson = PathUtilities.CreateTempSubdirectory();

            string tempProject = CreateDirectoryWithTempProject(
                packageId: packageId,
                versionRange: versionRange,
                targetFramework: string.IsNullOrEmpty(targetFramework) ? BundledTargetFramework.GetTargetFrameworkMoniker() : targetFramework,
                assetJsonOutputDirectory: new DirectoryPath(tempDirectoryForAssetJson),
                restoreDirectory: null,
                rootConfigDirectory: packageLocation.RootConfigDirectory,
                additionalFeeds: packageLocation.AdditionalFeeds);

            try
            {
                _projectRestorer.Restore(
                    new FilePath(tempProject),
                    packageLocation,
                    verbosity: verbosity);
            }
            finally
            {
                File.Delete(tempProject);
            }

            return ToolPackageInstance.CreateFromAssetFile(packageId, new DirectoryPath(tempDirectoryForAssetJson));
        }

        private string CreateDirectoryWithTempProject(
            PackageId packageId,
            VersionRange versionRange,
            string targetFramework,
            DirectoryPath? restoreDirectory,
            DirectoryPath assetJsonOutputDirectory,
            DirectoryPath? rootConfigDirectory,
            string[] additionalFeeds)
        {
            string tempProject;
            if (_tempProject != null && _tempProject.HasValue)
            {
                tempProject = _tempProject.Value.Value;
                Directory.CreateDirectory(Path.GetDirectoryName(tempProject));
            }
            else
                tempProject = Path.Combine(PathUtilities.CreateTempSubdirectory(), "restore.csproj");

            var tempProjectContent = new XDocument(
                new XElement("Project",
                    new XElement("PropertyGroup",
                        // due to https://github.com/Microsoft/msbuild/issues/1603 -- import SDK after setting MsBuildProjectExtensionsPath
                        new XElement("MsBuildProjectExtensionsPath", assetJsonOutputDirectory.Value)), // change the output directory of asset.json
                    new XElement(("Import"),
                        new XAttribute("Project", "Sdk.props"),
                        new XAttribute("Sdk", "Microsoft.NET.Sdk")),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", targetFramework),
                        restoreDirectory.HasValue ? new XElement("RestorePackagesPath", restoreDirectory.Value.Value) : null,
                        new XElement("RestoreProjectStyle", "DotnetToolReference"), // without it, project cannot reference tool package
                        new XElement("RestoreRootConfigDirectory", rootConfigDirectory?.Value ?? Directory.GetCurrentDirectory()), // config file probing start directory
                        new XElement("DisableImplicitFrameworkReferences", "true"), // no Microsoft.NETCore.App in tool folder
                        new XElement("RestoreFallbackFolders", "clear"), // do not use fallbackfolder, tool package need to be copied to tool folder
                        new XElement("RestoreAdditionalProjectSources", JoinSourceAndOfflineCache(additionalFeeds)),
                        new XElement("RestoreAdditionalProjectFallbackFolders", string.Empty), // block other
                        new XElement("RestoreAdditionalProjectFallbackFoldersExcludes", string.Empty),  // block other
                        new XElement("DisableImplicitNuGetFallbackFolder", "true")),  // disable SDK side implicit NuGetFallbackFolder
                     new XElement("ItemGroup",
                        new XElement("PackageReference",
                            new XAttribute("Include", packageId.ToString()),
                            new XAttribute("Version",
                                versionRange?.ToString("N", new VersionRangeFormatter()) ?? "*"))), // nuget will restore latest stable for * and format N is the normalization format
                    new XElement(("Import"),
                        new XAttribute("Project", "Sdk.targets"),
                        new XAttribute("Sdk", "Microsoft.NET.Sdk"))));

            File.WriteAllText(tempProject, tempProjectContent.ToString());
            return tempProject;
        }

        private string JoinSourceAndOfflineCache(string[] additionalFeeds)
        {
            var feeds = new List<string>();
            if (additionalFeeds != null)
            {
                foreach (var feed in additionalFeeds)
                {
                    if (Uri.IsWellFormedUriString(feed, UriKind.Absolute))
                    {
                        feeds.Add(feed);
                    }
                    else
                    {
                        feeds.Add(Path.GetFullPath(feed));
                    }
                }
            }

            // use fallbackfolder as feed to enable offline
            if (Directory.Exists(_offlineFeed.Value))
            {
                feeds.Add(_offlineFeed.ToXmlEncodeString());
            }

            return string.Join(";", feeds);
        }
    }
}
