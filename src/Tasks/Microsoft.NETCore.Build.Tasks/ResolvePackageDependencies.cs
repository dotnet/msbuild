// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Configuration;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Raises Nuget LockFile representation to MSBuild items and resolves
    /// assets specified in the lock file.
    /// </summary>
    public sealed class ResolvePackageDependencies : Task
    {
        private readonly List<string> _packageFolders = new List<string>();
        private readonly Dictionary<string, string> _fileTypes = new Dictionary<string, string>();
        private readonly HashSet<string> _projectFileDependencies = new HashSet<string>();
        private IPackageResolver _packageResolver;
        private LockFile _lockFile;

        #region Output Items

        private readonly List<ITaskItem> _targetDefinitions = new List<ITaskItem>();
        private readonly List<ITaskItem> _packageDefinitions = new List<ITaskItem>();
        private readonly List<ITaskItem> _fileDefinitions = new List<ITaskItem>();
        private readonly List<ITaskItem> _packageDependencies = new List<ITaskItem>();
        private readonly List<ITaskItem> _fileDependencies = new List<ITaskItem>();

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
        /// All the files in the lock file
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
        /// the file (e.g. CompileTimeAssembly, RuntimeAssembly, etc.)
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
        /// The lock file to process
        /// </summary>
        [Required]
        public string ProjectLockFile
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

        #endregion

        #region Test Support

        public ResolvePackageDependencies()
        {
        }

        public ResolvePackageDependencies(LockFile lockFile, IPackageResolver packageResolver)
            : this()
        {
            _lockFile = lockFile;
            _packageResolver = packageResolver;
        }

        #endregion

        private IPackageResolver PackageResolver
        {
            get
            {
                if (_packageResolver == null)
                {
                    NuGetPathContext nugetPathContext = NuGetPathContext.Create(Path.GetDirectoryName(ProjectPath));
                    _packageResolver = new NuGetPackageResolver(nugetPathContext);
                }

                return _packageResolver;
            }
        }

        private LockFile LockFile
        {
            get
            {
                if (_lockFile == null)
                {
                    if (!File.Exists(ProjectLockFile))
                    {
                        ReportException($"Lock file {ProjectLockFile} couldn't be found. Run a NuGet package restore to generate this file.");
                    }

                    _lockFile = new LockFileCache(BuildEngine4).GetLockFile(ProjectLockFile);
                }

                return _lockFile;
            }
        }

        /// <summary>
        /// Raise Nuget LockFile representation to MSBuild items
        /// </summary>
        public override bool Execute()
        {
            try
            {
                ExecuteCore();
                return true;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
                return false;
            }
        }

        private void ExecuteCore()
        {
            ReadProjectFileDependencies();
            RaiseLockFileTargets();
            GetPackageAndFileDefinitions();
        }

        private void ReadProjectFileDependencies()
        {
            foreach (var group in LockFile.ProjectFileDependencyGroups)
            {
                foreach (var dep in group.Dependencies)
                {
                    // Get package name from e.g. Microsoft.VSSDK.BuildTools >= 15.0.25604-Preview4
                    _projectFileDependencies.Add(dep.Split()[0].Trim());
                }
            }
        }

        // get library and file definitions
        private void GetPackageAndFileDefinitions()
        {
            TaskItem item;
            foreach (var package in LockFile.Libraries)
            {
                string packageId = $"{package.Name}/{package.Version.ToString()}";
                item = new TaskItem(packageId);
                item.SetMetadata(MetadataKeys.Name, package.Name);
                item.SetMetadata(MetadataKeys.Type, package.Type);
                item.SetMetadata(MetadataKeys.Version, package.Version.ToString());

                item.SetMetadata(MetadataKeys.Path, package.Path ?? string.Empty);

                string resolvedPackagePath = ResolvePackagePath(package);
                item.SetMetadata(MetadataKeys.ResolvedPath, resolvedPackagePath ?? string.Empty);

                _packageDefinitions.Add(item);

                foreach (var file in package.Files)
                {
                    if (Path.GetFileName(file) == "_._")
                    {
                        continue;
                    }

                    var fileKey = $"{packageId}/{file}";
                    var fileItem = new TaskItem(fileKey);
                    fileItem.SetMetadata(MetadataKeys.Path, file);

                    string resolvedPath = ResolveFilePath(file, resolvedPackagePath);
                    fileItem.SetMetadata(MetadataKeys.ResolvedPath, resolvedPath ?? string.Empty);

                    if (IsAnalyzer(file))
                    {
                        fileItem.SetMetadata(MetadataKeys.Analyzer, "true");
                        fileItem.SetMetadata(MetadataKeys.Type, "AnalyzerAssembly");

                        // get targets that contain this package
                        var parentTargets = LockFile.Targets
                            .Where(t => t.Libraries.Any(lib => lib.Name == package.Name));

                        foreach (var target in parentTargets)
                        {
                            var fileDepsItem = new TaskItem(fileKey);
                            fileDepsItem.SetMetadata(MetadataKeys.ParentTarget, target.Name); // Foreign Key
                            fileDepsItem.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                            _fileDependencies.Add(fileDepsItem);
                        }
                    }
                    else
                    {
                        // get a type for the file if one is available
                        string fileType;
                        if (!_fileTypes.TryGetValue(fileKey, out fileType))
                        {
                            fileType = "unknown";
                        }
                        fileItem.SetMetadata(MetadataKeys.Type, fileType);
                    }

                    _fileDefinitions.Add(fileItem);
                }
            }
        }

        private bool IsAnalyzer(string file)
        {
            bool isAnalyzer = false;

            if (file.StartsWith("analyzers", StringComparison.Ordinal)
                && Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var projectLanguage = GetLockFileLanguageName(ProjectLanguage);

                if (projectLanguage == "cs" || projectLanguage == "vb")
                {
                    string excludeLanguage = projectLanguage == "vb" ? "cs" : "vb";
                    var fileParts = file.Split('/');

                    isAnalyzer =
                        fileParts.Any(x => x.Equals(projectLanguage, StringComparison.OrdinalIgnoreCase)) ||
                        !fileParts.Any(x => x.Equals(excludeLanguage, StringComparison.OrdinalIgnoreCase));
                }
            }

            return isAnalyzer;
        }

        private static string GetLockFileLanguageName(string projectLanguage)
        {
            switch (projectLanguage)
            {
                case "C#": return "cs";
                case "F#": return "fs";
                default: return projectLanguage?.ToLowerInvariant();
            }
        }

        // get target definitions and package and file dependencies
        private void RaiseLockFileTargets()
        {
            TaskItem item;
            foreach (var target in LockFile.Targets)
            {
                item = new TaskItem(target.Name);
                item.SetMetadata(MetadataKeys.RuntimeIdentifier, target.RuntimeIdentifier ?? string.Empty);
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
            TaskItem item;
            foreach (var package in target.Libraries)
            {
                string packageId = $"{package.Name}/{package.Version.ToString()}";

                if (_projectFileDependencies.Contains(package.Name))
                {
                    item = new TaskItem(packageId);
                    item.SetMetadata(MetadataKeys.ParentTarget, target.Name); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentPackage, string.Empty); // Foreign Key

                    _packageDependencies.Add(item);
                }

                // get sub package dependencies
                GetPackageDependencies(package, target.Name);

                // get file dependencies on this package
                GetFileDependencies(package, target.Name);
            }
        }

        private void GetPackageDependencies(LockFileTargetLibrary package, string targetName)
        {
            string packageId = $"{package.Name}/{package.Version.ToString()}";
            TaskItem item;
            foreach (var deps in package.Dependencies)
            {
                string depsName = $"{deps.Id}/{deps.VersionRange.MinVersion.ToString()}";

                item = new TaskItem(depsName);
                item.SetMetadata(MetadataKeys.ParentTarget, targetName); // Foreign Key
                item.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                _packageDependencies.Add(item);
            }
        }

        private void GetFileDependencies(LockFileTargetLibrary package, string targetName)
        {
            string packageId = $"{package.Name}/{package.Version.ToString()}";
            TaskItem item;

            // for each type of file group
            foreach (var fileGroup in (FileGroup[])Enum.GetValues(typeof(FileGroup)))
            {
                var filePathList = fileGroup.GetFilePathListFor(package);
                foreach (var filePath in filePathList)
                {
                    if (Path.GetFileName(filePath) == "_._")
                    {
                        continue;
                    }

                    var fileKey = $"{packageId}/{filePath}";
                    item = new TaskItem(fileKey);
                    item.SetMetadata(MetadataKeys.FileGroup, fileGroup.ToString());
                    item.SetMetadata(MetadataKeys.ParentTarget, targetName); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

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
                string currentFileType;
                if (!_fileTypes.TryGetValue(fileKey, out currentFileType))
                {
                    _fileTypes.Add(fileKey, fileType);
                }
                else if (currentFileType != fileType)
                {
                    throw new Exception($"Unexpected file type for {fileKey}. Type is both {fileType} and {currentFileType}");
                }
            }
        }


        private string ResolvePackagePath(LockFileLibrary package)
        {
            if (package.Type == "project")
            {
                var relativeMSBuildProjectPath = package.MSBuildProject;

                if (string.IsNullOrEmpty(relativeMSBuildProjectPath))
                {
                    ReportException($"Your project is consuming assets from the project but no MSBuild project is found in the project.lock.json.");
                }

                return GetAbsolutePathFromProjectRelativePath(relativeMSBuildProjectPath);
            }
            else
            {
                return PackageResolver.GetPackageDirectory(package.Name, package.Version);
            }
        }

        private string ResolveFilePath(string relativePath, string resolvedPackagePath)
        {
            if (Path.GetFileName(relativePath) == "_._")
            {
                return null;
            }
            
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            return resolvedPackagePath != null
                ? Path.Combine(resolvedPackagePath, relativePath)
                : string.Empty;
        }

        private string GetAbsolutePathFromProjectRelativePath(string path)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ProjectLockFile)), path));
        }

        private void ReportException(string message)
        {
            throw new Exception(message);
        }
    }
}
