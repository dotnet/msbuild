// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.Extensions.Internal;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public struct LibraryAsset
    {
        public string Name { get; }
        public string RelativePath { get; }
        public string ResolvedPath { get; }
        public string FileName => Path.GetFileName(RelativePath);
        public Action<Stream, Stream> Transform { get; set; }

        public LibraryAsset(string name, string relativePath, string resolvedPath, Action<Stream, Stream> transform = null)
        {
            Name = name;
            RelativePath = relativePath;
            ResolvedPath = resolvedPath;
            Transform = transform;
        }

        public bool Equals(LibraryAsset other)
        {
            return string.Equals(Name, other.Name)
                && string.Equals(RelativePath, other.RelativePath)
                && string.Equals(ResolvedPath, other.ResolvedPath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is LibraryAsset && Equals((LibraryAsset) obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(Name);
            combiner.Add(RelativePath);
            combiner.Add(ResolvedPath);
            return combiner.CombinedHash;
        }

        public static LibraryAsset CreateFromRelativePath(string basePath, string relativePath, Action<Stream, Stream> transform = null)
        {
            return new LibraryAsset(
                    Path.GetFileNameWithoutExtension(relativePath),
                    relativePath,
                    Path.Combine(basePath, relativePath),
                    transform);
        }

        public static LibraryAsset CreateFromAbsolutePath(string basePath, string absolutePath, Action<Stream, Stream> transform = null)
        {
            var relativePath = absolutePath.Replace(PathUtility.EnsureTrailingSlash(basePath), string.Empty);

            return new LibraryAsset(
                    Path.GetFileNameWithoutExtension(relativePath),
                    relativePath,
                    absolutePath,
                    transform);
        }
    }
}
