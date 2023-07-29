// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestFinder : IToolManifestFinder, IToolManifestInspector
    {
        private readonly DirectoryPath _probeStart;
        private readonly IFileSystem _fileSystem;
        private readonly IDangerousFileDetector _dangerousFileDetector;
        private readonly ToolManifestEditor _toolManifestEditor;
        private const string ManifestFilenameConvention = "dotnet-tools.json";
        private readonly Func<string, string> _getEnvironmentVariable;

        public ToolManifestFinder(
            DirectoryPath probeStart,
            IFileSystem fileSystem = null,
            IDangerousFileDetector dangerousFileDetector = null,
            Func<string, string> getEnvironmentVariable = null)
        {
            _probeStart = probeStart;
            _fileSystem = fileSystem ?? new FileSystemWrapper();
            _dangerousFileDetector = dangerousFileDetector ?? new DangerousFileDetector();
            _toolManifestEditor = new ToolManifestEditor(_fileSystem, dangerousFileDetector);
            _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        }

        public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
        {
            IEnumerable<(FilePath manifestfile, DirectoryPath _)> allPossibleManifests =
                filePath != null
                    ? new[] {(filePath.Value, filePath.Value.GetDirectoryPath())}
                    : EnumerateDefaultAllPossibleManifests();

            var findAnyManifest =
                TryFindToolManifestPackages(allPossibleManifests, out var toolManifestPackageAndSource);

            if (!findAnyManifest)
            {
                throw new ToolManifestCannotBeFoundException(
                    LocalizableStrings.CannotFindAManifestFile,
                    string.Format(LocalizableStrings.ListOfSearched,
                        string.Join(Environment.NewLine, allPossibleManifests.Select(f => "\t" + f.manifestfile.Value))));
            }

            return toolManifestPackageAndSource.Select(t => t.toolManifestPackage).ToArray();
        }

        public IReadOnlyCollection<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> Inspect(
            FilePath? filePath = null)
        {
            IEnumerable<(FilePath manifestfile, DirectoryPath _)> allPossibleManifests =
                filePath != null
                    ? new[] {(filePath.Value, filePath.Value.GetDirectoryPath())}
                    : EnumerateDefaultAllPossibleManifests();


            if (!TryFindToolManifestPackages(allPossibleManifests, out var toolManifestPackageAndSource))
            {
                toolManifestPackageAndSource =
                    new List<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>();
            }

            return toolManifestPackageAndSource.ToArray();
        }

        private bool TryFindToolManifestPackages(
            IEnumerable<(FilePath manifestfile, DirectoryPath _)> allPossibleManifests, 
            out List<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> toolManifestPackageAndSource)
        {
            bool findAnyManifest = false;
            toolManifestPackageAndSource 
                = new List<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>();
            foreach ((FilePath possibleManifest, DirectoryPath correspondingDirectory) in allPossibleManifests)
            {
                if (!_fileSystem.File.Exists(possibleManifest.Value))
                {
                    continue;
                }

                findAnyManifest = true;

                (List<ToolManifestPackage> toolManifestPackageFromOneManifestFile, bool isRoot) =
                    _toolManifestEditor.Read(possibleManifest, correspondingDirectory);

                foreach (ToolManifestPackage toolManifestPackage in toolManifestPackageFromOneManifestFile)
                {
                    if (!toolManifestPackageAndSource.Any(addedToolManifestPackages =>
                        addedToolManifestPackages.toolManifestPackage.PackageId.Equals(toolManifestPackage.PackageId)))
                    {
                        toolManifestPackageAndSource.Add((toolManifestPackage, possibleManifest));
                    }
                }

                if (isRoot)
                {
                    return findAnyManifest;
                }
            }

            return findAnyManifest;
        }

        public bool TryFind(ToolCommandName toolCommandName, out ToolManifestPackage toolManifestPackage)
        {
            toolManifestPackage = default(ToolManifestPackage);
            foreach ((FilePath possibleManifest, DirectoryPath correspondingDirectory) in
                EnumerateDefaultAllPossibleManifests())
            {
                if (!_fileSystem.File.Exists(possibleManifest.Value))
                {
                    continue;
                }

                (List<ToolManifestPackage> manifestPackages, bool isRoot) =
                    _toolManifestEditor.Read(possibleManifest, correspondingDirectory);

                foreach (var package in manifestPackages)
                {
                    if (package.CommandNames.Contains(toolCommandName))
                    {
                        toolManifestPackage = package;
                        return true;
                    }
                }

                if (isRoot)
                {
                    return false;
                }
            }

            return false;
        }

        private IEnumerable<(FilePath manifestfile, DirectoryPath manifestFileFirstEffectDirectory)>
            EnumerateDefaultAllPossibleManifests()
        {
            DirectoryPath? currentSearchDirectory = _probeStart;
            while (currentSearchDirectory.HasValue && (currentSearchDirectory.Value.GetParentPathNullable() != null || AllowManifestInRoot()))
            {
                var currentSearchDotConfigDirectory =
                    currentSearchDirectory.Value.WithSubDirectories(Constants.DotConfigDirectoryName);
                var tryManifest = currentSearchDirectory.Value.WithFile(ManifestFilenameConvention);
                yield return (currentSearchDotConfigDirectory.WithFile(ManifestFilenameConvention),
                    currentSearchDirectory.Value);
                yield return (tryManifest, currentSearchDirectory.Value);
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private bool AllowManifestInRoot()
        {
            string environmentVariableValue = _getEnvironmentVariable(EnvironmentVariableNames.DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT);
            if (!string.IsNullOrWhiteSpace(environmentVariableValue))
            {
                if (environmentVariableValue.Equals("true", StringComparison.OrdinalIgnoreCase) || environmentVariableValue.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
        
        public FilePath FindFirst(bool createIfNotFound = false)
        {
            foreach ((FilePath possibleManifest, DirectoryPath _) in EnumerateDefaultAllPossibleManifests())
            {
                if (_fileSystem.File.Exists(possibleManifest.Value))
                {
                    return possibleManifest;
                }
            }
            if (createIfNotFound)
            {
                DirectoryPath manifestInsertFolder = GetDirectoryToCreateToolManifest();
                if (manifestInsertFolder.Value != null)
                {
                    return new FilePath(WriteManifestFile(manifestInsertFolder));
                }
            }
            throw new ToolManifestCannotBeFoundException(
                    LocalizableStrings.CannotFindAManifestFile,
                    string.Format(LocalizableStrings.ListOfSearched,
                        string.Join(Environment.NewLine,
                            EnumerateDefaultAllPossibleManifests().Select(f => "\t" + f.manifestfile.Value))));
        }

        /*
        The --create-manifest-if-needed will use the following priority to choose the folder where the tool manifest goes:
            1. Walk up the directory tree searching for one that has a.git subfolder
            2. Walk up the directory tree searching for one that has a .sln/git file in it
            3. Use the current working directory
        */
        private DirectoryPath GetDirectoryToCreateToolManifest()
        {
            DirectoryPath? currentSearchDirectory = _probeStart;
            while (currentSearchDirectory.HasValue && currentSearchDirectory.Value.GetParentPathNullable()!=null)
            {
                var currentSearchGitDirectory = currentSearchDirectory.Value.WithSubDirectories(Constants.GitDirectoryName);
                if (_fileSystem.Directory.Exists(currentSearchGitDirectory.Value))
                {
                    return currentSearchDirectory.Value;
                }
                if (currentSearchDirectory.Value.Value != null)
                {
                    if (_fileSystem.Directory.EnumerateFiles(currentSearchDirectory.Value.Value)
                        .Any(filename => Path.GetExtension(filename).Equals(".sln", StringComparison.OrdinalIgnoreCase))
                        || _fileSystem.File.Exists(currentSearchDirectory.Value.WithFile(".git").Value))

                    {
                        return currentSearchDirectory.Value;
                    }
                }
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
            return _probeStart;
        }

        private string WriteManifestFile(DirectoryPath folderPath)
        {
            var manifestFileContent = """
                {
                  "version": 1,
                  "isRoot": true,
                  "tools": {}
                }
                """;
            _fileSystem.Directory.CreateDirectory(Path.Combine(folderPath.Value, Constants.DotConfigDirectoryName));
            string manifestFileLocation = Path.Combine(folderPath.Value, Constants.DotConfigDirectoryName, Constants.ToolManifestFileName);
            _fileSystem.File.WriteAllText(manifestFileLocation, manifestFileContent);

            return manifestFileLocation;
        }

        /// <summary>
        /// Return manifest file path in the order of the closest probe path first.
        /// </summary>
        public IReadOnlyList<FilePath> FindByPackageId(PackageId packageId)
        {
            var result = new List<FilePath>();
            bool findAnyManifest = false;
            foreach ((FilePath possibleManifest,
                    DirectoryPath correspondingDirectory)
                in EnumerateDefaultAllPossibleManifests())
            {
                if (_fileSystem.File.Exists(possibleManifest.Value))
                {
                    findAnyManifest = true;
                    if (_toolManifestEditor.Read(possibleManifest, correspondingDirectory).content
                        .Any(t => t.PackageId.Equals(packageId)))
                    {
                        result.Add(possibleManifest);
                    }
                }
            }

            if (!findAnyManifest)
            {
                throw new ToolManifestCannotBeFoundException(
                    LocalizableStrings.CannotFindAManifestFile,
                    string.Format(LocalizableStrings.ListOfSearched,
                        string.Join(Environment.NewLine,
                            EnumerateDefaultAllPossibleManifests().Select(f => "\t" + f.manifestfile.Value))));
            }

            return result;
        }
    }
}
