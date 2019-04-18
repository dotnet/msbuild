// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.IO;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    internal enum ConflictItemType
    {
        Reference,
        CopyLocal,
        Runtime,
        Platform
    }

    internal interface IConflictItem
    {
        Version AssemblyVersion { get; }
        ConflictItemType ItemType { get; }
        bool Exists { get; }
        string FileName { get; }
        Version FileVersion { get; }
        string PackageId { get; }
        string DisplayName { get; }

        // NOTE: Technically this should be NuGetVersion because System.Version doesn't work with semver.
        // However, the only scenarios we need to support this property for in conflict resolution is stable versions
        // of System packages. PackageVersion will be null if System.Version can't parse the version (i.e. if is pre-release)
        Version PackageVersion { get; }
    }

    // Wraps an ITask item and adds lazy evaluated properties used by Conflict resolution.
    internal class ConflictItem : IConflictItem
    {
        public ConflictItem(ITaskItem originalItem, ConflictItemType itemType)
        {
            OriginalItem = originalItem;
            ItemType = itemType;
        }

        public ConflictItem(string fileName, string packageId, Version assemblyVersion, Version fileVersion)
        {
            OriginalItem = null;
            ItemType = ConflictItemType.Platform;
            FileName = fileName;
            SourcePath = fileName;
            PackageId = packageId;
            AssemblyVersion = assemblyVersion;
            FileVersion = fileVersion;
        }

        private bool _hasAssemblyVersion;
        private Version _assemblyVersion;
        public Version AssemblyVersion
        {
            get
            {
                if (!_hasAssemblyVersion)
                {
                    _assemblyVersion = null;

                    var assemblyVersionString = OriginalItem?.GetMetadata(nameof(AssemblyVersion)) ?? String.Empty;

                    if (assemblyVersionString.Length != 0)
                    {
                        Version.TryParse(assemblyVersionString, out _assemblyVersion);
                    }
                    else
                    {
                        _assemblyVersion = FileUtilities.TryGetAssemblyVersion(SourcePath);
                    }

                    // assemblyVersion may be null but don't try to recalculate it
                    _hasAssemblyVersion = true;
                }

                return _assemblyVersion;
            }
            private set
            {
                _assemblyVersion = value;
                _hasAssemblyVersion = true;
            }
        }

        public ConflictItemType ItemType { get; }

        private bool? _exists;
        public bool Exists
        {
            get
            {
                if (_exists == null)
                {
                    _exists = ItemType == ConflictItemType.Platform || File.Exists(SourcePath);
                }

                return _exists.Value;
            }
        }

        private string _fileName;
        public string FileName
        {
            get
            {
                if (_fileName == null)
                {
                    _fileName = OriginalItem == null ? String.Empty : Path.GetFileName(OriginalItem.ItemSpec);
                }
                return _fileName;
            }
            private set { _fileName = value; }
        }

        private bool _hasFileVersion;
        private Version _fileVersion;
        public Version FileVersion
        {
            get
            {
                if (!_hasFileVersion)
                {
                    _fileVersion = null;

                    var fileVersionString = OriginalItem?.GetMetadata(nameof(FileVersion)) ?? String.Empty;

                    if (fileVersionString.Length != 0)
                    {
                        Version.TryParse(fileVersionString, out _fileVersion);
                    }
                    else
                    {
                        _fileVersion = FileUtilities.GetFileVersion(SourcePath);
                    }

                    // fileVersion may be null but don't try to recalculate it
                    _hasFileVersion = true;
                }

                return _fileVersion;
            }
            private set
            {
                _fileVersion = value;
                _hasFileVersion = true;
            }
        }

        public ITaskItem OriginalItem { get; }

        private string _packageId;
        public string PackageId
        {
            get
            {
                if (_packageId == null)
                {
                    _packageId = OriginalItem?.GetMetadata(MetadataNames.NuGetPackageId);

                    if (string.IsNullOrEmpty(_packageId))
                    {
                        _packageId = OriginalItem?.GetMetadata(MetadataKeys.PackageName) ?? string.Empty;
                    }

                    if (_packageId.Length == 0)
                    {
                        //  We want to move away from using the heuristic of walking up the folder tree until
                        //  we find a .nuspec in order to determine the package ID of a file.  However, we
                        //  don't want to accidentally stop having a package ID for a file that the heuristic
                        //  would have found the package ID for.  So to catch those cases, we throw an
                        //  exception if the heuristic finds a package ID but we don't have the package ID
                        //  from any other source
                        string packageIdFromPath = NuGetUtils.GetPackageIdFromSourcePath(SourcePath);
                        if (!string.IsNullOrEmpty(packageIdFromPath))
                        {
                            string path = OriginalItem?.ItemSpec;
                            if (string.IsNullOrEmpty(path))
                            {
                                path = SourcePath;
                            }
                            throw new InvalidOperationException("NuGetPackageId metadata not set on " + path);
                        }

                        _packageId = string.Empty;
                    }
                }

                return _packageId.Length == 0 ? null : _packageId;
            }
            private set { _packageId = value; }
        }

        private bool _hasPackageVersion;
        private Version _packageVersion;
        public Version PackageVersion
        {
            get
            {
                if (!_hasPackageVersion)
                {
                    _packageVersion = null;

                    var packageVersionString = OriginalItem?.GetMetadata(nameof(MetadataNames.NuGetPackageVersion)) ?? String.Empty;

                    if (packageVersionString.Length != 0)
                    {
                        Version.TryParse(packageVersionString, out _packageVersion);
                    }

                    // PackageVersion may be null but don't try to recalculate it
                    _hasPackageVersion = true;
                }

                return _packageVersion;
            }
        }

        private string _sourcePath;
        public string SourcePath
        {
            get
            {
                if (_sourcePath == null)
                {
                    _sourcePath = ItemUtilities.GetSourcePath(OriginalItem) ?? String.Empty;
                }

                return _sourcePath.Length == 0 ? null : _sourcePath;
            }
            private set { _sourcePath = value; }
        }
        
        private string _displayName;
        public string DisplayName
        {
            get
            {
                if (_displayName == null)
                {
                    var itemSpec = OriginalItem == null ? FileName : OriginalItem.ItemSpec;
                    _displayName = $"{ItemType}:{itemSpec}";
                }
                return _displayName;
            }
        }
    }
}
