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
    public sealed class LockFileToMSBuild : Task
    {
        #region Output Items

        private readonly List<ITaskItem> _lockFileLibraries = new List<ITaskItem>();
        private readonly List<ITaskItem> _lockFileTargets = new List<ITaskItem>();
        private readonly List<ITaskItem> _lockFileTargetLibraries = new List<ITaskItem>();
        private readonly List<ITaskItem> _lockFileItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _packageDependencies = new List<ITaskItem>();

        /// <summary>
        /// All the libraries in the lock file.
        /// </summary>
        [Output]
        public ITaskItem[] LockFileLibraries
        {
            get { return _lockFileLibraries.ToArray(); }
        }

        /// <summary>
        /// All the targets in the lock file.
        /// </summary>
        [Output]
        public ITaskItem[] LockFileTargets
        {
            get { return _lockFileTargets.ToArray(); }
        }

        /// <summary>
        /// Libraries in each target
        /// </summary>
        [Output]
        public ITaskItem[] LockFileTargetLibraries
        {
            get { return _lockFileTargetLibraries.ToArray(); }
        }

        /// <summary>
        /// File items in each target library grouped into compile time assemblies,
        /// run time assemblies, content files etc.
        /// </summary>
        [Output]
        public ITaskItem[] LockFileItems
        {
            get { return _lockFileItems.ToArray(); }
        }

        /// <summary>
        /// Dependencies for each target library
        /// </summary>
        [Output]
        public ITaskItem[] PackageDependencies
        {
            get { return _packageDependencies.ToArray(); }
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

            RaiseLockFileLibraries(lockFile);
            RaiseLockFileTargets(lockFile);
        }

        private void RaiseLockFileLibraries(NuGet.ProjectModel.LockFile lockFile)
        {
            TaskItem item;
            foreach (var library in lockFile.Libraries)
            {
                item = new TaskItem(library.Path ?? library.Name); // TODO canonical item spec
                item.SetMetadata(MetadataKeys.Name, library.Name);
                item.SetMetadata(MetadataKeys.Type, library.Type);
                item.SetMetadata(MetadataKeys.Version, library.Version.ToString());

                _lockFileLibraries.Add(item);
            }
        }

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

                _lockFileTargets.Add(item);

                // raise each library in the target
                RaiseLockFileTargetLibraries(target);
            }
        }

        private void RaiseLockFileTargetLibraries(LockFileTarget target)
        {
            TaskItem item;
            foreach (var library in target.Libraries)
            {
                item = new TaskItem(library.Name);
                item.SetMetadata(MetadataKeys.Version, library.Version.ToString());
                item.SetMetadata(MetadataKeys.Type, library.Type);
                item.SetMetadata(MetadataKeys.ParentTarget, target.Name); // Foreign Key

                _lockFileTargetLibraries.Add(item);

                // raise all the file types
                RaiseLockFileItems(library, target.Name);
                RaisePackageDependencies(library, target.Name);
            }
        }

        private void RaiseLockFileItems(LockFileTargetLibrary library, string targetName)
        {
            TaskItem item;

            // for each type of file group
            foreach (var fileGroup in (FileGroup[])Enum.GetValues(typeof(FileGroup)))
            {
                var fileItemList = GetFileItemListFor(library, fileGroup);
                foreach (var fileItem in fileItemList)
                {
                    item = new TaskItem(fileItem.Path);
                    item.SetMetadata(MetadataKeys.FileGroup, fileGroup.ToString());
                    item.SetMetadata(MetadataKeys.ParentTarget, targetName); // Foreign Key
                    item.SetMetadata(MetadataKeys.ParentTargetLibrary, library.Name); // Foreign Key

                    _lockFileItems.Add(item);
                }
            }
        }

        private void RaisePackageDependencies(LockFileTargetLibrary library, string targetName)
        {
            TaskItem item;
            foreach (var deps in library.Dependencies)
            {
                item = new TaskItem(deps.Id);
                item.SetMetadata(MetadataKeys.Version, deps.VersionRange.OriginalString);
                item.SetMetadata(MetadataKeys.ParentTarget, targetName); // Foreign Key
                item.SetMetadata(MetadataKeys.ParentTargetLibrary, library.Name); // Foreign Key

                _packageDependencies.Add(item);
            }
        }

        private IList<LockFileItem> GetFileItemListFor(LockFileTargetLibrary library, FileGroup fileGroup)
        {
            switch (fileGroup)
            {
                case FileGroup.CompileTimeAssembly:
                    return library.CompileTimeAssemblies;

                case FileGroup.RuntimeAssembly:
                    return library.RuntimeAssemblies;

                case FileGroup.ContentFile:
                    return library.ContentFiles;

                case FileGroup.NativeLibrary:
                    return library.NativeLibraries;

                case FileGroup.ResourceAssembly:
                    return library.ResourceAssemblies;

                case FileGroup.RuntimeTarget:
                    return library.RuntimeTargets;

                default:
                    ReportException(null); return null;
            }
        }

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
