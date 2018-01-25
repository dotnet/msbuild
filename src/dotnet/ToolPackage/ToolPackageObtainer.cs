using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageObtainer : IToolPackageObtainer
    {
        private readonly Lazy<string> _bundledTargetFrameworkMoniker;
        private readonly Func<FilePath> _getTempProjectPath;
        private readonly IProjectRestorer _projectRestorer;
        private readonly DirectoryPath _toolsPath;
        private readonly DirectoryPath _offlineFeedPath;

        public ToolPackageObtainer(
            DirectoryPath toolsPath,
            DirectoryPath offlineFeedPath,
            Func<FilePath> getTempProjectPath,
            Lazy<string> bundledTargetFrameworkMoniker,
            IProjectRestorer projectRestorer
        )
        {
            _getTempProjectPath = getTempProjectPath;
            _bundledTargetFrameworkMoniker = bundledTargetFrameworkMoniker;
            _projectRestorer = projectRestorer ?? throw new ArgumentNullException(nameof(projectRestorer));
            _toolsPath = toolsPath;
            _offlineFeedPath = offlineFeedPath;
        }

        public ToolConfigurationAndExecutablePath ObtainAndReturnExecutablePath(
            string packageId,
            string packageVersion = null,
            FilePath? nugetconfig = null,
            string targetframework = null,
            string source = null)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (nugetconfig != null)
            {
                if (!File.Exists(nugetconfig.Value.Value))
                {
                    throw new PackageObtainException(
                        string.Format(CommonLocalizableStrings.NuGetConfigurationFileDoesNotExist,
                            Path.GetFullPath(nugetconfig.Value.Value)));
                }
            }

            if (targetframework == null)
            {
                targetframework = _bundledTargetFrameworkMoniker.Value;
            }

            var packageVersionOrPlaceHolder = new PackageVersion(packageVersion);

            DirectoryPath toolDirectory =
                CreateIndividualToolVersionDirectory(packageId, packageVersionOrPlaceHolder);

            FilePath tempProjectPath = CreateTempProject(
                packageId,
                packageVersionOrPlaceHolder,
                targetframework,
                toolDirectory);

            _projectRestorer.Restore(tempProjectPath, toolDirectory, nugetconfig, source);

            if (packageVersionOrPlaceHolder.IsPlaceholder)
            {
                var concreteVersion =
                    new DirectoryInfo(
                        Directory.GetDirectories(
                            toolDirectory.WithSubDirectories(packageId).Value).Single()).Name;
                DirectoryPath versioned =
                    toolDirectory.GetParentPath().WithSubDirectories(concreteVersion);

                MoveToVersionedDirectory(versioned, toolDirectory);

                toolDirectory = versioned;
                packageVersion = concreteVersion;
            }

            LockFile lockFile = new LockFileFormat()
                .ReadWithLock(toolDirectory.WithFile("project.assets.json").Value)
                .Result;

            LockFileItem dotnetToolSettings = FindAssetInLockFile(lockFile, "DotnetToolSettings.xml", packageId);

            if (dotnetToolSettings == null)
            {
                throw new PackageObtainException(
                    string.Format(CommonLocalizableStrings.ToolPackageMissingSettingsFile, packageId));
            }

            FilePath toolConfigurationPath =
                toolDirectory
                    .WithSubDirectories(packageId, packageVersion)
                    .WithFile(dotnetToolSettings.Path);

            ToolConfiguration toolConfiguration =
                ToolConfigurationDeserializer.Deserialize(toolConfigurationPath.Value);

            var entryPointFromLockFile =
                FindAssetInLockFile(lockFile, toolConfiguration.ToolAssemblyEntryPoint, packageId);

            if (entryPointFromLockFile == null)
            {
                throw new PackageObtainException(string.Format(CommonLocalizableStrings.ToolPackageMissingEntryPointFile,
                    packageId, toolConfiguration.ToolAssemblyEntryPoint));
            }

            return new ToolConfigurationAndExecutablePath(
                toolConfiguration,
                toolDirectory.WithSubDirectories(
                        packageId,
                        packageVersion)
                    .WithFile(entryPointFromLockFile.Path));
        }

        private static LockFileItem FindAssetInLockFile(
            LockFile lockFile,
            string targetRelativeFilePath, string packageId)
        {
            return lockFile
                .Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l => l.Name == packageId)
                ?.ToolsAssemblies
                ?.SingleOrDefault(t => LockFileMatcher.MatchesFile(t, targetRelativeFilePath));
        }

        private static void MoveToVersionedDirectory(
            DirectoryPath versioned,
            DirectoryPath temporary)
        {
            if (Directory.Exists(versioned.Value))
            {
                Directory.Delete(versioned.Value, recursive: true);
            }

            Directory.Move(temporary.Value, versioned.Value);
        }

        private FilePath CreateTempProject(
            string packageId,
            PackageVersion packageVersion,
            string targetframework,
            DirectoryPath individualToolVersion)
        {
            FilePath tempProjectPath = _getTempProjectPath();
            if (Path.GetExtension(tempProjectPath.Value) != "csproj")
            {
                tempProjectPath = new FilePath(Path.ChangeExtension(tempProjectPath.Value, "csproj"));
            }

            EnsureDirectoryExists(tempProjectPath.GetDirectoryPath());
            var tempProjectContent = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", targetframework),
                        new XElement("RestorePackagesPath", individualToolVersion.Value), // tool package will restore to tool folder
                        new XElement("RestoreProjectStyle", "DotnetToolReference"), // without it, project cannot reference tool package
                        new XElement("RestoreRootConfigDirectory", Directory.GetCurrentDirectory()), // config file probing start directory
                        new XElement("DisableImplicitFrameworkReferences", "true"), // no Microsoft.NETCore.App in tool folder
                        new XElement("RestoreFallbackFolders", "clear"), // do not use fallbackfolder, tool package need to be copied to tool folder
                        new XElement("RestoreAdditionalProjectSources", // use fallbackfolder as feed to enable offline
                            Directory.Exists(_offlineFeedPath.Value) ? _offlineFeedPath.Value : string.Empty),
                        new XElement("RestoreAdditionalProjectFallbackFolders", string.Empty), // block other
                        new XElement("RestoreAdditionalProjectFallbackFoldersExcludes", string.Empty),  // block other
                        new XElement("DisableImplicitNuGetFallbackFolder", "true")),  // disable SDK side implicit NuGetFallbackFolder
                     new XElement("ItemGroup",
                        new XElement("PackageReference",
                            new XAttribute("Include", packageId),
                            new XAttribute("Version", packageVersion.IsConcreteValue ? packageVersion.Value : "*") // nuget will restore * for latest
                            ))
                        ));

            File.WriteAllText(tempProjectPath.Value,
                tempProjectContent.ToString());

            return tempProjectPath;
        }

        private DirectoryPath CreateIndividualToolVersionDirectory(
            string packageId,
            PackageVersion packageVersion)
        {
            DirectoryPath individualTool = _toolsPath.WithSubDirectories(packageId);
            DirectoryPath individualToolVersion = individualTool.WithSubDirectories(packageVersion.Value);
            EnsureDirectoryExists(individualToolVersion);
            return individualToolVersion;
        }

        private static void EnsureDirectoryExists(DirectoryPath path)
        {
            if (!Directory.Exists(path.Value))
            {
                Directory.CreateDirectory(path.Value);
            }
        }
    }
}
