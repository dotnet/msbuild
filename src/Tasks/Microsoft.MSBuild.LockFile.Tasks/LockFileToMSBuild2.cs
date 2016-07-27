using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.MSBuild.LockFile.Tasks
{
    /// <summary>
    /// Raises Nuget LockFile representation to MSBuild items and resolves
    /// assets specified in the lock file.
    /// </summary>
    public sealed class LockFileToMSBuild2 : Task
    {
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
        /// The lock file to process
        /// </summary>
        [Required]
        public string ProjectLockFile
        {
            get; set;
        }

        /// <summary>
        /// Filter processed items to those that match specified frameworks.
        /// If this is not provided, all frameworks are returned.
        /// </summary>
        public string[] TargetFrameworks
        {
            get; set;
        }

        /// <summary>
        /// Filter processed items to those that match specified runtime identifiers.
        /// If this is not provided, all RIDs are used.
        /// </summary>
        public string[] RuntimeIdentifiers
        {
            get; set;
        }

        #endregion

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
            var lockFile = GetLockFile();

            GetPackageAndFileDefinitions(lockFile);
            RaiseLockFileTargets(lockFile);
        }

        // get library and file definitions
        private void GetPackageAndFileDefinitions(NuGet.ProjectModel.LockFile lockFile)
        {
            TaskItem item;
            foreach (var package in lockFile.Libraries)
            {
                string packageId = $"{package.Name}/{package.Version.ToString()}";
                item = new TaskItem(packageId); 
                item.SetMetadata(MetadataKeys.Name, package.Name);
                item.SetMetadata(MetadataKeys.Type, package.Type);
                item.SetMetadata(MetadataKeys.Version, package.Version.ToString());
                if (package.Path != null)
                {
                    item.SetMetadata(MetadataKeys.Path, package.Path);
                    // todo resolved path
                }
                _packageDefinitions.Add(item);

                foreach (var file in package.Files)
                {
                    var fileItem = new TaskItem($"{packageId}/{file}");
                    fileItem.SetMetadata(MetadataKeys.Path, file);
                    // todo resolvedPath
                    // todo analyzer
                    _fileDefinitions.Add(fileItem);
                }
            }
        }

        // get target definitions and package and file dependencies
        private void RaiseLockFileTargets(NuGet.ProjectModel.LockFile lockFile)
        {
            TaskItem item;
            foreach (var target in lockFile.Targets)
            {
                item = new TaskItem(target.Name);
                item.SetMetadata(MetadataKeys.RuntimeIdentifier, target.RuntimeIdentifier ?? string.Empty);
                item.SetMetadata(MetadataKeys.TargetFramework, target.TargetFramework.DotNetFrameworkName);
                item.SetMetadata(MetadataKeys.FrameworkName, target.TargetFramework.Framework);
                item.SetMetadata(MetadataKeys.FrameworkVersion, target.TargetFramework.Version.ToString());

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
                item = new TaskItem(packageId);
                item.SetMetadata(MetadataKeys.ParentTarget, target.Name); // Foreign Key
                item.SetMetadata(MetadataKeys.ParentPackage, string.Empty); // Foreign Key

                _packageDependencies.Add(item);

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
                string depsName = $"{deps.Id}/{deps.VersionRange.OriginalString}";

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
                var filePathList = GetFilePathListFor(package, fileGroup);
                foreach (var filePath in filePathList)
                {
                    item = new TaskItem($"{packageId}/{filePath}");
                    item.SetMetadata(MetadataKeys.FileGroup, fileGroup.ToString());
                    item.SetMetadata(MetadataKeys.ParentTarget, targetName); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentPackage, packageId); // Foreign Key

                    _fileDependencies.Add(item);
                }
            }
        }

        private IEnumerable<string> GetFilePathListFor(LockFileTargetLibrary package, FileGroup fileGroup)
        {
            switch (fileGroup)
            {
                case FileGroup.CompileTimeAssembly:
                    return SelectPath(package.CompileTimeAssemblies);

                case FileGroup.RuntimeAssembly:
                    return SelectPath(package.RuntimeAssemblies);

                case FileGroup.ContentFile:
                    return SelectPath(package.ContentFiles);

                case FileGroup.NativeLibrary:
                    return SelectPath(package.NativeLibraries);

                case FileGroup.ResourceAssembly:
                    return SelectPath(package.ResourceAssemblies);

                case FileGroup.RuntimeTarget:
                    return SelectPath(package.RuntimeTargets);

                case FileGroup.FrameworkAssembly:
                    return package.FrameworkAssemblies;

                default:
                    ReportException(null); return null;
            }
        }

        private IEnumerable<string> SelectPath(IList<LockFileItem> fileItemList) 
            => fileItemList.Select(c => c.Path);

        private NuGet.ProjectModel.LockFile GetLockFile()
        {
            if (!File.Exists(ProjectLockFile))
            {
                ReportException("Could not find lock file");
            }

            // TODO adapt task logger to Nuget Logger
            return LockFileUtilities.GetLockFile(ProjectLockFile, NuGet.Logging.NullLogger.Instance);
        }

        private void ReportException(string message)
        {
            throw new Exception(message);
        }
    }
}
