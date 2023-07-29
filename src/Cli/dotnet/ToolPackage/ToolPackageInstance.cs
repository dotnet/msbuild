// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ToolPackage
{
    // This is named "ToolPackageInstance" because "ToolPackage" would conflict with the namespace
    internal class ToolPackageInstance : IToolPackage
    {
        public static ToolPackageInstance CreateFromAssetFile(PackageId id, DirectoryPath assetsJsonParentDirectory)
        {
            var lockFile = new LockFileFormat().Read(assetsJsonParentDirectory.WithFile(AssetsFileName).Value);
            var packageDirectory = new DirectoryPath(lockFile.PackageFolders[0].Path);
            var library = FindLibraryInLockFile(lockFile, id);
            var version = library.Version;

            return new ToolPackageInstance(id, version, packageDirectory, assetsJsonParentDirectory);
        }
        private const string PackagedShimsDirectoryConvention = "shims";

        public IEnumerable<string> Warnings => _toolConfiguration.Value.Warnings;

        public PackageId Id { get; private set; }

        public NuGetVersion Version { get; private set; }

        public DirectoryPath PackageDirectory { get; private set; }

        public IReadOnlyList<RestoredCommand> Commands
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

        public IEnumerable<NuGetFramework> Frameworks { get; private set; }

        private const string AssetsFileName = "project.assets.json";
        private const string ToolSettingsFileName = "DotnetToolSettings.xml";

        private Lazy<IReadOnlyList<RestoredCommand>> _commands;
        private Lazy<ToolConfiguration> _toolConfiguration;
        private Lazy<LockFile> _lockFile;
        private Lazy<IReadOnlyList<FilePath>> _packagedShims;

        public ToolPackageInstance(PackageId id,
            NuGetVersion version,
            DirectoryPath packageDirectory,
            DirectoryPath assetsJsonParentDirectory)
        {
            _commands = new Lazy<IReadOnlyList<RestoredCommand>>(GetCommands);
            _packagedShims = new Lazy<IReadOnlyList<FilePath>>(GetPackagedShims);

            Id = id;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PackageDirectory = packageDirectory;
            _toolConfiguration = new Lazy<ToolConfiguration>(GetToolConfiguration);
            _lockFile =
                new Lazy<LockFile>(
                    () => new LockFileFormat().Read(assetsJsonParentDirectory.WithFile(AssetsFileName).Value));
            var toolsPackagePath = Path.Combine(PackageDirectory.Value, Id.ToString(), Version.ToNormalizedString(), "tools");
            Frameworks = Directory.GetDirectories(toolsPackagePath)
                .Select(path => NuGetFramework.ParseFolder(Path.GetFileName(path)));
        }

        private IReadOnlyList<RestoredCommand> GetCommands()
        {
            try
            {
                var commands = new List<RestoredCommand>();
                LockFileTargetLibrary library = FindLibraryInLockFile(_lockFile.Value);
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
                commands.Add(new RestoredCommand(
                    new ToolCommandName(configuration.CommandName),
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
                var library = FindLibraryInLockFile(_lockFile.Value);
                return DeserializeToolConfiguration(library);
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
                library = FindLibraryInLockFile(_lockFile.Value);
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

        private ToolConfiguration DeserializeToolConfiguration(LockFileTargetLibrary library)
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

        private static LockFileTargetLibrary FindLibraryInLockFile(LockFile lockFile, PackageId id)
        {
            return lockFile
                ?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, id.ToString(), StringComparison.OrdinalIgnoreCase) == 0);
        }

        private LockFileTargetLibrary FindLibraryInLockFile(LockFile lockFile)
        {
            return FindLibraryInLockFile(lockFile, Id);
        }

        private static LockFileItem FindItemInTargetLibrary(LockFileTargetLibrary library, string targetRelativeFilePath)
        {
            return library
                ?.ToolsAssemblies
                ?.SingleOrDefault(t => LockFileMatcher.MatchesFile(t, targetRelativeFilePath));
        }

    }
}
