// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    internal enum ConflictItemType
    {
        Reference,
        CopyLocal,
        Runtime,
        Platform
    }
    // Wraps an ITask item and adds lazy evaluated properties used by Conflict resolution.
    internal class ConflictItem
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

        private bool hasAssemblyVersion;
        private Version assemblyVersion;
        public Version AssemblyVersion
        {
            get
            {
                if (!hasAssemblyVersion)
                {
                    assemblyVersion = null;

                    var assemblyVersionString = OriginalItem?.GetMetadata(nameof(AssemblyVersion)) ?? String.Empty;

                    if (assemblyVersionString.Length != 0)
                    {
                        Version.TryParse(assemblyVersionString, out assemblyVersion);
                    }
                    else
                    {
                        assemblyVersion = FileUtilities.TryGetAssemblyVersion(SourcePath);
                    }

                    // assemblyVersion may be null but don't try to recalculate it
                    hasAssemblyVersion = true;
                }

                return assemblyVersion;
            }
            private set
            {
                assemblyVersion = value;
                hasAssemblyVersion = true;
            }
        }

        public ConflictItemType ItemType { get; }

        private bool? exists;
        public bool Exists
        {
            get
            {
                if (exists == null)
                {
                    exists = ItemType == ConflictItemType.Platform || File.Exists(SourcePath);
                }

                return exists.Value;
            }
        }

        private string fileName;
        public string FileName
        {
            get
            {
                if (fileName == null)
                {
                    fileName = OriginalItem == null ? String.Empty : OriginalItem.GetMetadata(MetadataNames.FileName) + OriginalItem.GetMetadata(MetadataNames.Extension);
                }
                return fileName;
            }
            private set { fileName = value; }
        }

        private bool hasFileVersion;
        private Version fileVersion;
        public Version FileVersion
        {
            get
            {
                if (!hasFileVersion)
                {
                    fileVersion = null;

                    var fileVersionString = OriginalItem?.GetMetadata(nameof(FileVersion)) ?? String.Empty;

                    if (fileVersionString.Length != 0)
                    {
                        Version.TryParse(fileVersionString, out fileVersion);
                    }
                    else
                    {
                        fileVersion = FileUtilities.GetFileVersion(SourcePath);
                    }

                    // fileVersion may be null but don't try to recalculate it
                    hasFileVersion = true;
                }

                return fileVersion;
            }
            private set
            {
                fileVersion = value;
                hasFileVersion = true;
            }
        }

        public ITaskItem OriginalItem { get; }

        private string packageId;
        public string PackageId
        {
            get
            {
                if (packageId == null)
                {
                    packageId = OriginalItem?.GetMetadata(MetadataNames.NuGetPackageId) ?? String.Empty;

                    if (packageId.Length == 0)
                    {
                        packageId = NuGetUtilities.GetPackageIdFromSourcePath(SourcePath) ?? String.Empty;
                    }
                }

                return packageId.Length == 0 ? null : packageId;
            }
            private set { packageId = value; }
        }
        

        private string sourcePath;
        public string SourcePath
        {
            get
            {
                if (sourcePath == null)
                {
                    sourcePath = ItemUtilities.GetSourcePath(OriginalItem) ?? String.Empty;
                }

                return sourcePath.Length == 0 ? null : sourcePath;
            }
            private set { sourcePath = value; }
        }
        
        public string displayName;
        public string DisplayName
        {
            get
            {
                if (displayName == null)
                {
                    var itemSpec = OriginalItem == null ? FileName : OriginalItem.ItemSpec;
                    displayName = $"{ItemType}:{itemSpec}";
                }
                return displayName;
            }
        }
    }
}
