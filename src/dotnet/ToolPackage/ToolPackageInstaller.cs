using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageInstaller : IToolPackageInstaller
    {
        public const string StagingDirectory = ".stage";

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
            _offlineFeed = offlineFeed ?? new DirectoryPath(new CliFolderPathCalculator().CliFallbackFolderPath);
        }

        public IToolPackage InstallPackage(
            string packageId,
            string packageVersion = null,
            string targetFramework = null,
            FilePath? nugetConfig = null,
            string source = null,
            string verbosity = null)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            var packageRootDirectory = _store.Root.WithSubDirectories(packageId);
            string rollbackDirectory = null;

            return TransactionalAction.Run<IToolPackage>(
                action: () => {
                    try
                    {

                        var stageDirectory = _store.Root.WithSubDirectories(StagingDirectory, Path.GetRandomFileName());
                        Directory.CreateDirectory(stageDirectory.Value);
                        rollbackDirectory = stageDirectory.Value;

                        var tempProject = CreateTempProject(
                            packageId: packageId,
                            packageVersion: packageVersion,
                            targetFramework: targetFramework ?? BundledTargetFramework.GetTargetFrameworkMoniker(),
                            restoreDirectory: stageDirectory);

                        try
                        {
                            _projectRestorer.Restore(
                                tempProject,
                                stageDirectory,
                                nugetConfig,
                                source,
                                verbosity);
                        }
                        finally
                        {
                            File.Delete(tempProject.Value);
                        }

                        packageVersion = Path.GetFileName(
                            Directory.EnumerateDirectories(
                                stageDirectory.WithSubDirectories(packageId).Value).Single());

                        var packageDirectory = packageRootDirectory.WithSubDirectories(packageVersion);
                        if (Directory.Exists(packageDirectory.Value))
                        {
                            throw new ToolPackageException(
                                string.Format(
                                    CommonLocalizableStrings.ToolPackageConflictPackageId,
                                    packageId,
                                    packageVersion));
                        }

                        Directory.CreateDirectory(packageRootDirectory.Value);
                        Directory.Move(stageDirectory.Value, packageDirectory.Value);
                        rollbackDirectory = packageDirectory.Value;

                        return new ToolPackageInstance(
                            _store,
                            packageId,
                            packageVersion,
                            packageDirectory);
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
                rollback: () => {
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

        private FilePath CreateTempProject(
            string packageId,
            string packageVersion,
            string targetFramework,
            DirectoryPath restoreDirectory)
        {
            var tempProject = _tempProject ?? new DirectoryPath(Path.GetTempPath())
                .WithSubDirectories(Path.GetRandomFileName())
                .WithFile(Path.GetRandomFileName() + ".csproj");

            if (Path.GetExtension(tempProject.Value) != "csproj")
            {
                tempProject = new FilePath(Path.ChangeExtension(tempProject.Value, "csproj"));
            }

            Directory.CreateDirectory(tempProject.GetDirectoryPath().Value);

            var tempProjectContent = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", targetFramework),
                        new XElement("RestorePackagesPath", restoreDirectory.Value),
                        new XElement("RestoreProjectStyle", "DotnetToolReference"), // without it, project cannot reference tool package
                        new XElement("RestoreRootConfigDirectory", Directory.GetCurrentDirectory()), // config file probing start directory
                        new XElement("DisableImplicitFrameworkReferences", "true"), // no Microsoft.NETCore.App in tool folder
                        new XElement("RestoreFallbackFolders", "clear"), // do not use fallbackfolder, tool package need to be copied to tool folder
                        new XElement("RestoreAdditionalProjectSources", // use fallbackfolder as feed to enable offline
                            Directory.Exists(_offlineFeed.Value) ? _offlineFeed.Value : string.Empty),
                        new XElement("RestoreAdditionalProjectFallbackFolders", string.Empty), // block other
                        new XElement("RestoreAdditionalProjectFallbackFoldersExcludes", string.Empty),  // block other
                        new XElement("DisableImplicitNuGetFallbackFolder", "true")),  // disable SDK side implicit NuGetFallbackFolder
                     new XElement("ItemGroup",
                        new XElement("PackageReference",
                            new XAttribute("Include", packageId),
                            new XAttribute("Version", packageVersion ?? "*") // nuget will restore * for latest
                            ))
                        ));

            File.WriteAllText(tempProject.Value, tempProjectContent.ToString());
            return tempProject;
        }
    }
}
