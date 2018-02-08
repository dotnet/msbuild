using System;
using System.IO;
using System.Linq;
using System.Transactions;
using System.Xml.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
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
            string source = null,
            string verbosity = null)
        {
            var stageDirectory = _toolsPath.WithSubDirectories(".stage", Path.GetRandomFileName());

            var toolPackageObtainTransaction = new ToolPackageObtainTransaction(
                obtainAndReturnExecutablePath: (locationOfPackageDuringTransaction) =>
                {
                    if (Directory.Exists(_toolsPath.WithSubDirectories(packageId).Value))
                    {
                        throw new PackageObtainException(
                            string.Format(CommonLocalizableStrings.ToolPackageConflictPackageId, packageId));
                    }

                    locationOfPackageDuringTransaction.Add(stageDirectory);
                    var toolConfigurationAndExecutablePath = ObtainAndReturnExecutablePathInStageFolder(
                                                                 packageId,
                                                                 stageDirectory,
                                                                 packageVersion,
                                                                 nugetconfig,
                                                                 targetframework,
                                                                 source,
                                                                 verbosity);

                    DirectoryPath destinationDirectory = _toolsPath.WithSubDirectories(packageId);

                    Directory.Move(
                      stageDirectory.Value,
                      destinationDirectory.Value);

                    locationOfPackageDuringTransaction.Clear();
                    locationOfPackageDuringTransaction.Add(destinationDirectory);

                    return toolConfigurationAndExecutablePath;
                },
                rollback: (locationOfPackageDuringTransaction) =>
                {
                    foreach (DirectoryPath l in locationOfPackageDuringTransaction)
                    {
                        if (Directory.Exists(l.Value))
                        {
                            Directory.Delete(l.Value, recursive: true);
                        }
                    }
                }
            );

            using (var transactionScope = new TransactionScope())
            {
                Transaction.Current.EnlistVolatile(toolPackageObtainTransaction, EnlistmentOptions.None);
                var toolConfigurationAndExecutablePath = toolPackageObtainTransaction.ObtainAndReturnExecutablePath();

                transactionScope.Complete();
                return toolConfigurationAndExecutablePath;
            }
        }

        private ToolConfigurationAndExecutablePath ObtainAndReturnExecutablePathInStageFolder(
            string packageId,
            DirectoryPath stageDirectory,
            string packageVersion = null,
            FilePath? nugetconfig = null,
            string targetframework = null,
            string source = null,
            string verbosity = null)
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

            DirectoryPath nugetSandboxDirectory =
                CreateNugetSandboxDirectory(packageVersionOrPlaceHolder, stageDirectory);

            FilePath tempProjectPath = CreateTempProject(
                packageId,
                packageVersionOrPlaceHolder,
                targetframework,
                nugetSandboxDirectory);

            _projectRestorer.Restore(tempProjectPath, nugetSandboxDirectory, nugetconfig, source, verbosity);

            if (packageVersionOrPlaceHolder.IsPlaceholder)
            {
                var concreteVersion =
                    new DirectoryInfo(
                        Directory.GetDirectories(
                            nugetSandboxDirectory.WithSubDirectories(packageId).Value).Single()).Name;
                DirectoryPath versioned =
                    nugetSandboxDirectory.GetParentPath().WithSubDirectories(concreteVersion);

                MoveToVersionedDirectory(versioned, nugetSandboxDirectory);

                nugetSandboxDirectory = versioned;
                packageVersion = concreteVersion;
            }

            LockFile lockFile = new LockFileFormat()
                .ReadWithLock(nugetSandboxDirectory.WithFile("project.assets.json").Value)
                .Result;

            LockFileItem dotnetToolSettings = FindAssetInLockFile(lockFile, "DotnetToolSettings.xml", packageId);

            if (dotnetToolSettings == null)
            {
                throw new PackageObtainException(
                    string.Format(CommonLocalizableStrings.ToolPackageMissingSettingsFile, packageId));
            }

            FilePath toolConfigurationPath =
                nugetSandboxDirectory
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
                _toolsPath.WithSubDirectories(
                        packageId,
                        packageVersion,
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

        private DirectoryPath CreateNugetSandboxDirectory(
            PackageVersion packageVersion,
            DirectoryPath stageDirectory
        )
        {
            DirectoryPath individualToolVersion = stageDirectory.WithSubDirectories(packageVersion.Value);
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
