// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Raises Nuget LockFile representation to MSBuild items and resolves
    /// assets specified in the lock file.
    /// </summary>
    /// <remarks>
    /// Only called for backwards compatability, when <c>ResolvePackageDependencies</c> is true.
    /// </remarks>
    public sealed class ResolvePackageDependencies : TaskBase
    {
        private readonly Dictionary<string, string> _fileTypes = new(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> _projectFileDependencies;
        private IPackageResolver _packageResolver;
        private LockFile _lockFile;

        #region Output Items

        private readonly List<ITaskItem> _targetDefinitions = new();
        private readonly List<ITaskItem> _packageDefinitions = new();
        private readonly List<ITaskItem> _fileDefinitions = new();
        private readonly List<ITaskItem> _packageDependencies = new();
        private readonly List<ITaskItem> _fileDependencies = new();

        /// <summary>
        /// All the targets in the lock file.
        /// </summary>
        [Output]
        public ITaskItem[] TargetDefinitions
        {
            get { return _targetDefinitions.ToArray(); }
        }

        /// <summary>
        /// All the libraries/packages in the lock file.
        /// </summary>
        [Output]
        public ITaskItem[] PackageDefinitions
        {
            get { return _packageDefinitions.ToArray(); }
        }

        /// <summary>
        /// All the files in the lock file.
        /// </summary>
        [Output]
        public ITaskItem[] FileDefinitions
        {
            get { return _fileDefinitions.ToArray(); }
        }

        /// <summary>
        /// All the dependencies between packages. Each package has metadata 'ParentPackage'
        /// to refer to the package that depends on it. For top level packages this value is blank.
        /// </summary>
        [Output]
        public ITaskItem[] PackageDependencies
        {
            get { return _packageDependencies.ToArray(); }
        }

        /// <summary>
        /// All the dependencies between files and packages, labeled by the group containing
        /// the file (e.g. CompileTimeAssembly, RuntimeAssembly, etc.).
        /// </summary>
        [Output]
        public ITaskItem[] FileDependencies
        {
            get { return _fileDependencies.ToArray(); }
        }

        #endregion

        #region Inputs

        /// <summary>
        /// The path to the current project.
        /// </summary>
        [Required]
        public string ProjectPath
        {
            get; set;
        }

        /// <summary>
        /// The assets file to process
        /// </summary>
        public string ProjectAssetsFile
        {
            get; set;
        }

        /// <summary>
        /// Optional the Project Language (E.g. C#, VB)
        /// </summary>
        public string ProjectLanguage
        {
            get; set;
        }

        public string TargetFramework { get; set; }

        #endregion

        public ResolvePackageDependencies()
        {
        }

        #region Test Support

        internal ResolvePackageDependencies(LockFile lockFile, IPackageResolver packageResolver)
            : this()
        {
            _lockFile = lockFile;
            _packageResolver = packageResolver;
        }

        #endregion

        private IPackageResolver PackageResolver => _packageResolver ??= NuGetPackageResolver.CreateResolver(LockFile);

        private LockFile LockFile => _lockFile ??= new LockFileCache(this).GetLockFile(ProjectAssetsFile);

        private Dictionary<string, string> _targetNameToAliasMap;

        /// <summary>
        /// Raise Nuget LockFile representation to MSBuild items
        /// </summary>
        protected override void ExecuteCore()
        {
            _targetNameToAliasMap = LockFile.Targets.ToDictionary(t => t.Name, t =>
            {
                var alias = LockFile.GetLockFileTargetAlias(t);
                if (string.IsNullOrEmpty(t.RuntimeIdentifier))
                {
                    return alias;
                }
                else
                {
                    return alias + "/" + t.RuntimeIdentifier;
                }
            });

            ReadProjectFileDependencies(string.IsNullOrEmpty(TargetFramework) || !_targetNameToAliasMap.ContainsKey(TargetFramework) ? null : _targetNameToAliasMap[TargetFramework]);
            RaiseLockFileTargets();
            GetPackageAndFileDefinitions();
        }

        private void ReadProjectFileDependencies(string frameworkAlias)
        {
            _projectFileDependencies = LockFile.GetProjectFileDependencySet(frameworkAlias);
        }

        // get library and file definitions
        private void GetPackageAndFileDefinitions()
        {
            foreach (var package in LockFile.Libraries)
            {
                var packageName = package.Name;
                var packageVersion = package.Version.ToNormalizedString();
                string packageId = $"{packageName}/{packageVersion}";
                var item = new TaskItem(packageId);
                item.SetMetadata(MetadataKeys.Name, packageName);
                item.SetMetadata(MetadataKeys.Type, package.Type);
                item.SetMetadata(MetadataKeys.Version, packageVersion);

                item.SetMetadata(MetadataKeys.Path, package.Path ?? string.Empty);

                string resolvedPackagePath = ResolvePackagePath(package);
                item.SetMetadata(MetadataKeys.ResolvedPath, resolvedPackagePath ?? string.Empty);

                item.SetMetadata(MetadataKeys.DiagnosticLevel, GetPackageDiagnosticLevel(package));

                _packageDefinitions.Add(item);

                foreach (var file in package.Files)
                {
                    if (NuGetUtils.IsPlaceholderFile(file))
                    {
                        continue;
                    }

                    var fileKey = $"{packageId}/{file}";
                    var fileItem = new TaskItem(fileKey);
                    fileItem.SetMetadata(MetadataKeys.Path, file);
                    fileItem.SetMetadata(MetadataKeys.NuGetPackageId, packageName);
                    fileItem.SetMetadata(MetadataKeys.NuGetPackageVersion, packageVersion);

                    string resolvedPath = ResolveFilePath(file, resolvedPackagePath);
                    fileItem.SetMetadata(MetadataKeys.ResolvedPath, resolvedPath ?? string.Empty);

                    if (NuGetUtils.IsApplicableAnalyzer(file, ProjectLanguage))
                    {
                        fileItem.SetMetadata(MetadataKeys.Analyzer, "true");
                        fileItem.SetMetadata(MetadataKeys.Type, "AnalyzerAssembly");

                        // get targets that contain this package
                        var parentTargets = LockFile.Targets
                            .Where(t => t.Libraries.Any(lib => lib.Name == package.Name));

                        foreach (var target in parentTargets)
                        {
                            string frameworkAlias = _targetNameToAliasMap[target.Name];

                            var fileDepsItem = new TaskItem(fileKey);
                            fileDepsItem.SetMetadata(MetadataKeys.ParentTarget, frameworkAlias); // Foreign Key
                            fileDepsItem.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                            _fileDependencies.Add(fileDepsItem);
                        }
                    }
                    else
                    {
                        // get a type for the file if one is available
                        if (!_fileTypes.TryGetValue(fileKey, out string fileType))
                        {
                            fileType = "unknown";
                        }
                        fileItem.SetMetadata(MetadataKeys.Type, fileType);
                    }

                    _fileDefinitions.Add(fileItem);
                }
            }

            string GetPackageDiagnosticLevel(LockFileLibrary package)
            {
                string target = TargetFramework ?? "";

                var messages = LockFile.LogMessages.Where(log => log.LibraryId == package.Name && log.TargetGraphs
                                .Select(tg =>
                                {
                                    var parsedTargetGraph = NuGetFramework.Parse(tg);
                                    var alias = _lockFile.PackageSpec.TargetFrameworks.FirstOrDefault(tf => tf.FrameworkName == parsedTargetGraph)?.TargetAlias;
                                    return alias ?? tg;
                                }).Contains(target));

                if (!messages.Any())
                {
                    return string.Empty;
                }

                return messages.Max(log => log.Level).ToString();
            }
        }

        // get target definitions and package and file dependencies
        private void RaiseLockFileTargets()
        {
            foreach (var target in LockFile.Targets)
            {
                TaskItem item = new(target.Name);
                item.SetMetadata(MetadataKeys.RuntimeIdentifier, target.RuntimeIdentifier ?? string.Empty);
                item.SetMetadata(MetadataKeys.TargetFramework, TargetFramework);
                item.SetMetadata(MetadataKeys.TargetFrameworkMoniker, target.TargetFramework.DotNetFrameworkName);
                item.SetMetadata(MetadataKeys.FrameworkName, target.TargetFramework.Framework);
                item.SetMetadata(MetadataKeys.FrameworkVersion, target.TargetFramework.Version.ToString());
                item.SetMetadata(MetadataKeys.Type, "target");

                _targetDefinitions.Add(item);

                // raise each library in the target
                GetPackageAndFileDependencies(target);
            }
        }

        private void GetPackageAndFileDependencies(LockFileTarget target)
        {
            var resolvedPackageVersions = target.Libraries
                .ToDictionary(pkg => pkg.Name, pkg => pkg.Version.ToNormalizedString(), StringComparer.OrdinalIgnoreCase);

            string frameworkAlias = _targetNameToAliasMap[target.Name];

            var transitiveProjectRefs = new HashSet<string>(
                target.Libraries
                    .Where(lib => lib.IsTransitiveProjectReference(LockFile, ref _projectFileDependencies, frameworkAlias))
                    .Select(pkg => pkg.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var package in target.Libraries)
            {
                string packageId = $"{package.Name}/{package.Version.ToNormalizedString()}";

                if (_projectFileDependencies.Contains(package.Name))
                {
                    TaskItem item = new(packageId);
                    item.SetMetadata(MetadataKeys.ParentTarget, frameworkAlias); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentPackage, string.Empty); // Foreign Key

                    _packageDependencies.Add(item);
                }

                // get sub package dependencies
                GetPackageDependencies(package, target.Name, resolvedPackageVersions, transitiveProjectRefs);

                // get file dependencies on this package
                GetFileDependencies(package, target.Name);
            }
        }

        private void GetPackageDependencies(
            LockFileTargetLibrary package,
            string targetName,
            Dictionary<string, string> resolvedPackageVersions,
            HashSet<string> transitiveProjectRefs)
        {
            string packageId = $"{package.Name}/{package.Version.ToNormalizedString()}";
            string frameworkAlias = _targetNameToAliasMap[targetName];
            foreach (var deps in package.Dependencies)
            {
                if (!resolvedPackageVersions.TryGetValue(deps.Id, out string version))
                {
                    continue;
                }

                string depsName = $"{deps.Id}/{version}";

                TaskItem item = new(depsName);
                item.SetMetadata(MetadataKeys.ParentTarget, frameworkAlias); // Foreign Key
                item.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                if (transitiveProjectRefs.Contains(deps.Id))
                {
                    item.SetMetadata(MetadataKeys.TransitiveProjectReference, "true");
                }

                _packageDependencies.Add(item);
            }
        }

        private void GetFileDependencies(LockFileTargetLibrary package, string targetName)
        {
            string packageId = $"{package.Name}/{package.Version.ToNormalizedString()}";
            string frameworkAlias = _targetNameToAliasMap[targetName];

            // for each type of file group
            foreach (var fileGroup in (FileGroup[])Enum.GetValues(typeof(FileGroup)))
            {
                var filePathList = fileGroup.GetFilePathAndProperties(package);
                foreach (var entry in filePathList)
                {
                    string filePath = entry.Item1;
                    IDictionary<string, string> properties = entry.Item2;

                    if (NuGetUtils.IsPlaceholderFile(filePath))
                    {
                        continue;
                    }

                    var fileKey = $"{packageId}/{filePath}";
                    var item = new TaskItem(fileKey);
                    item.SetMetadata(MetadataKeys.FileGroup, fileGroup.ToString());
                    item.SetMetadata(MetadataKeys.ParentTarget, frameworkAlias); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                    if (fileGroup == FileGroup.FrameworkAssembly)
                    {
                        // NOTE: the path provided for framework assemblies is the name of the framework reference
                        item.SetMetadata("FrameworkAssembly", filePath);
                        item.SetMetadata(MetadataKeys.NuGetPackageId, package.Name);
                        item.SetMetadata(MetadataKeys.NuGetPackageVersion, package.Version.ToNormalizedString());
                    }

                    foreach (var property in properties)
                    {
                        item.SetMetadata(property.Key, property.Value);
                    }

                    _fileDependencies.Add(item);

                    // map each file key to a Type metadata value
                    SaveFileKeyType(fileKey, fileGroup);
                }
            }
        }

        // save file type metadata based on the group the file appears in
        private void SaveFileKeyType(string fileKey, FileGroup fileGroup)
        {
            string fileType = fileGroup.GetTypeMetadata();
            if (fileType != null)
            {
                if (!_fileTypes.TryGetValue(fileKey, out string currentFileType))
                {
                    _fileTypes.Add(fileKey, fileType);
                }
                else if (currentFileType != fileType)
                {
                    throw new BuildErrorException(Strings.UnexpectedFileType, fileKey, fileType, currentFileType);
                }
            }
        }

        private string ResolvePackagePath(LockFileLibrary package)
        {
            if (package.IsProject())
            {
                var relativeMSBuildProjectPath = package.MSBuildProject;

                if (string.IsNullOrEmpty(relativeMSBuildProjectPath))
                {
                    throw new BuildErrorException(Strings.ProjectAssetsConsumedWithoutMSBuildProjectPath, package.Name, ProjectAssetsFile);
                }

                return GetAbsolutePathFromProjectRelativePath(relativeMSBuildProjectPath);
            }
            else
            {
                return PackageResolver.GetPackageDirectory(package.Name, package.Version);
            }
        }

        private static string ResolveFilePath(string relativePath, string resolvedPackagePath)
        {
            if (NuGetUtils.IsPlaceholderFile(relativePath))
            {
                return null;
            }

            if (resolvedPackagePath == null)
            {
                return string.Empty;
            }

            if (Path.DirectorySeparatorChar != '/')
            {
                relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            }

            if (Path.DirectorySeparatorChar != '\\')
            {
                relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            }

            return Path.Combine(resolvedPackagePath, relativePath);
        }

        private string GetAbsolutePathFromProjectRelativePath(string path)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectPath), path));
        }
    }
}
