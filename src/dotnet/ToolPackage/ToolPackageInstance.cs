using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.Utils;
using System.Threading;

namespace Microsoft.DotNet.ToolPackage
{
    // This is named "ToolPackageInstance" because "ToolPackage" would conflict with the namespace
    internal class ToolPackageInstance : IToolPackage
    {
        private const string PackagedShimsDirectoryConvention = "shims";

        public IEnumerable<string> Warnings => _toolConfiguration.Value.Warnings;

        public PackageId Id { get; private set; }

        public NuGetVersion Version { get; private set; }

        public DirectoryPath PackageDirectory { get; private set; }

        public IReadOnlyList<CommandSettings> Commands
        {
            get
            {
                return _commands.Value;
            }
        }

        public IReadOnlyList<FilePath> PackagedShims
        {
            get
            {
                return _packagedShims.Value;
            }
        }

        private const string AssetsFileName = "project.assets.json";
        private const string ToolSettingsFileName = "DotnetToolSettings.xml";

        private IToolPackageStore _store;
        private Lazy<IReadOnlyList<CommandSettings>> _commands;
        private Lazy<ToolConfiguration> _toolConfiguration;
        private Lazy<IReadOnlyList<FilePath>> _packagedShims;

        public ToolPackageInstance(
            IToolPackageStore store,
            PackageId id,
            NuGetVersion version,
            DirectoryPath packageDirectory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _commands = new Lazy<IReadOnlyList<CommandSettings>>(GetCommands);
            _packagedShims = new Lazy<IReadOnlyList<FilePath>>(GetPackagedShims);

            Id = id;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PackageDirectory = packageDirectory;
            _toolConfiguration = new Lazy<ToolConfiguration>(GetToolConfiguration);
        }

        public void Uninstall()
        {
            var rootDirectory = PackageDirectory.GetParentPath();
            string tempPackageDirectory = null;

            TransactionalAction.Run(
                action: () =>
                {
                    try
                    {
                        if (Directory.Exists(PackageDirectory.Value))
                        {
                            // Use the staging directory for uninstall
                            // This prevents cross-device moves when temp is mounted to a different device
                            var tempPath = _store.GetRandomStagingDirectory().Value;
                            FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(PackageDirectory.Value, tempPath));
                            tempPackageDirectory = tempPath;
                        }

                        if (Directory.Exists(rootDirectory.Value) &&
                            !Directory.EnumerateFileSystemEntries(rootDirectory.Value).Any())
                        {
                            Directory.Delete(rootDirectory.Value, false);
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CommonLocalizableStrings.FailedToUninstallToolPackage,
                                Id,
                                ex.Message),
                            ex);
                    }
                },
                commit: () =>
                {
                    if (tempPackageDirectory != null)
                    {
                        Directory.Delete(tempPackageDirectory, true);
                    }
                },
                rollback: () =>
                {
                    if (tempPackageDirectory != null)
                    {
                        Directory.CreateDirectory(rootDirectory.Value);
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(tempPackageDirectory, PackageDirectory.Value));
                    }
                });
        }

        private IReadOnlyList<CommandSettings> GetCommands()
        {
            try
            {
                var commands = new List<CommandSettings>();
                LockFile lockFile = new LockFileFormat().Read(PackageDirectory.WithFile(AssetsFileName).Value);
                LockFileTargetLibrary library = FindLibraryInLockFile(lockFile);

                ToolConfiguration configuration = _toolConfiguration.Value;
                LockFileItem entryPointFromLockFile = FindItemInTargetLibrary(library, configuration.ToolAssemblyEntryPoint);
                if (entryPointFromLockFile == null)
                {
                    throw new ToolConfigurationException(
                        string.Format(
                            CommonLocalizableStrings.MissingToolEntryPointFile,
                            configuration.ToolAssemblyEntryPoint,
                            configuration.CommandName));
                }

                // Currently only "dotnet" commands are supported
                commands.Add(new CommandSettings(
                    configuration.CommandName,
                    "dotnet",
                    LockFileRelativePathToFullFilePath(entryPointFromLockFile.Path, library)));

                return commands;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.FailedToRetrieveToolConfiguration,
                        ex.Message),
                    ex);
            }
        }

        private FilePath LockFileRelativePathToFullFilePath(string lockFileRelativePath, LockFileTargetLibrary library)
        {
            return PackageDirectory
                        .WithSubDirectories(
                            Id.ToString(),
                            library.Version.ToNormalizedString())
                        .WithFile(lockFileRelativePath);
        }

        private ToolConfiguration GetToolConfiguration()
        {
            try
            {
                var lockFile = new LockFileFormat().Read(PackageDirectory.WithFile(AssetsFileName).Value);
                var library = FindLibraryInLockFile(lockFile);
                return DeserializeToolConfiguration(ToolSettingsFileName, library);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.FailedToRetrieveToolConfiguration,
                        ex.Message),
                    ex);
            }
        }

        private IReadOnlyList<FilePath> GetPackagedShims()
        {
            LockFileTargetLibrary library;
            try
            {
                LockFile lockFile = new LockFileFormat().Read(PackageDirectory.WithFile(AssetsFileName).Value);
                library = FindLibraryInLockFile(lockFile);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.FailedToReadNuGetLockFile,
                        Id,
                        ex.Message),
                    ex);
            }

            IEnumerable<LockFileItem> filesUnderShimsDirectory = library
                ?.ToolsAssemblies
                ?.Where(t => LockFileMatcher.MatchesDirectoryPath(t, PackagedShimsDirectoryConvention));

            if (filesUnderShimsDirectory == null)
            {
                return Array.Empty<FilePath>();
            }

            IEnumerable<string> allAvailableShimRuntimeIdentifiers = filesUnderShimsDirectory
                .Select(f => f.Path.Split('\\', '/')?[4]) // ex: "tools/netcoreapp2.1/any/shims/osx-x64/demo" osx-x64 is at [4]
                .Where(f => !string.IsNullOrEmpty(f));

            if (new FrameworkDependencyFile().TryGetMostFitRuntimeIdentifier(
                DotnetFiles.VersionFileObject.BuildRid,
                allAvailableShimRuntimeIdentifiers.ToArray(),
                out var mostFitRuntimeIdentifier))
            {
                return library
                           ?.ToolsAssemblies
                           ?.Where(l =>
                               LockFileMatcher.MatchesDirectoryPath(l, $"{PackagedShimsDirectoryConvention}/{mostFitRuntimeIdentifier}"))
                           .Select(l => LockFileRelativePathToFullFilePath(l.Path, library)).ToArray()
                       ?? Array.Empty<FilePath>();
            }
            else
            {
                return Array.Empty<FilePath>();
            }
        }

        private ToolConfiguration DeserializeToolConfiguration(string ToolSettingsFileName, LockFileTargetLibrary library)
        {
            var dotnetToolSettings = FindItemInTargetLibrary(library, ToolSettingsFileName);
            if (dotnetToolSettings == null)
            {
                throw new ToolConfigurationException(
                    CommonLocalizableStrings.MissingToolSettingsFile);
            }

            var toolConfigurationPath =
                PackageDirectory
                    .WithSubDirectories(
                        Id.ToString(),
                        library.Version.ToNormalizedString())
                    .WithFile(dotnetToolSettings.Path);

            var configuration = ToolConfigurationDeserializer.Deserialize(toolConfigurationPath.Value);
            return configuration;
        }

        private LockFileTargetLibrary FindLibraryInLockFile(LockFile lockFile)
        {
            return lockFile
                ?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, Id.ToString(), StringComparison.CurrentCultureIgnoreCase) == 0);
        }

        private static LockFileItem FindItemInTargetLibrary(LockFileTargetLibrary library, string targetRelativeFilePath)
        {
            return library
                ?.ToolsAssemblies
                ?.SingleOrDefault(t => LockFileMatcher.MatchesFile(t, targetRelativeFilePath));
        }

    }
}
