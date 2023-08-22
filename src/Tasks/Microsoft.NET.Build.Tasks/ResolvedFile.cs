// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Build.Tasks
{
    internal enum AssetType
    {
        None,
        Runtime,
        Native,
        Resources,
        PgoData
    }

    internal class ResolvedFile
    {
        public string SourcePath { get; }
        public string PackageName { get; }
        public string PackageVersion { get; }
        public string PathInPackage { get; }
        public string DestinationSubDirectory { get; }
        public AssetType Asset { get; }
        public bool IsRuntimeTarget { get; }
        public string RuntimeIdentifier { get; }
        public string Culture { get; }
        public string FileName
        {
            get { return Path.GetFileName(SourcePath); }
        }

        public string DestinationSubPath
        {
            get
            {
                return string.IsNullOrEmpty(DestinationSubDirectory) ?
                      FileName :
                      Path.Combine(DestinationSubDirectory, FileName);
            }
        }

        public ResolvedFile(string sourcePath, string destinationSubDirectory, PackageIdentity package,
            AssetType assetType = AssetType.None,
            string pathInPackage = null)
        {
            SourcePath = Path.GetFullPath(sourcePath);
            DestinationSubDirectory = destinationSubDirectory;
            Asset = assetType;
            PackageName = package.Id;
            PackageVersion = package.Version.ToString();
            PathInPackage = pathInPackage;
        }

        public ResolvedFile(ITaskItem item, bool isRuntimeTarget)
        {
            SourcePath = item.ItemSpec;
            DestinationSubDirectory = item.GetMetadata(MetadataKeys.DestinationSubDirectory);
            string assetType = item.GetMetadata(MetadataKeys.AssetType);
            if (assetType.Equals("runtime", StringComparison.OrdinalIgnoreCase))
            {
                Asset = AssetType.Runtime;
            }
            else if (assetType.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                Asset = AssetType.Native;
            }
            else if (assetType.Equals("resources", StringComparison.OrdinalIgnoreCase))
            {
                Asset = AssetType.Resources;
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized AssetType '{assetType}' for {SourcePath}");
            }

            PackageName = item.GetMetadata(MetadataKeys.NuGetPackageId);

            PackageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);

            PathInPackage = item.GetMetadata(MetadataKeys.PathInPackage);

            RuntimeIdentifier = item.GetMetadata(MetadataKeys.RuntimeIdentifier);
            Culture = item.GetMetadata(MetadataKeys.Culture);

            IsRuntimeTarget = isRuntimeTarget;

        }

        public override bool Equals(object obj)
        {
            ResolvedFile other = obj as ResolvedFile;
            return other != null &&
                other.Asset == Asset &&
                other.SourcePath == SourcePath &&
                other.DestinationSubDirectory == DestinationSubDirectory;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return SourcePath.GetHashCode() + DestinationSubDirectory.GetHashCode();
            }
        }
    }
}
