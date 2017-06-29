// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System;
using NuGet.Packaging.Core;
namespace Microsoft.NET.Build.Tasks
{
    internal enum AssetType
    {
        None,
        Runtime,
        Native,
        Resources
    }

    internal class ResolvedFile
    {
        public string SourcePath { get; }
        public PackageIdentity Package { get; }
        public string DestinationSubDirectory { get; }
        public AssetType Asset{ get; }
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

        public ResolvedFile(string sourcePath, string destinationSubDirectory, PackageIdentity package, AssetType assetType = AssetType.None)
        {
            SourcePath = Path.GetFullPath(sourcePath);
            DestinationSubDirectory = destinationSubDirectory;
            Asset = assetType;
            Package = package;

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
