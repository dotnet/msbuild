// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace NuGet.Legacy
{
    public class PhysicalPackageFile : IPackageFile
    {
        private readonly Func<Stream> _streamFactory;

        public PhysicalPackageFile()
        {
        }

        public PhysicalPackageFile(PhysicalPackageFile file)
        {
            SourcePath = file.SourcePath;
            TargetPath = file.TargetPath;
        }

        internal PhysicalPackageFile(Func<Stream> streamFactory)
        {
            _streamFactory = streamFactory;
        }

        /// <summary>
        /// Path on disk
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Path in package
        /// </summary>
        public string TargetPath { get; set; }

        public string Path
        {
            get
            {
                return TargetPath;
            }
        }

        public Stream GetStream()
        {
            return _streamFactory != null ? _streamFactory() : File.OpenRead(SourcePath);
        }

        public override string ToString()
        {
            return TargetPath;
        }

        public override bool Equals(object obj)
        {
            var file = obj as PhysicalPackageFile;

            return file != null && string.Equals(SourcePath, file.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(TargetPath, file.TargetPath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (SourcePath != null)
            {
                hash = SourcePath.GetHashCode();
            }

            if (TargetPath != null)
            {
                hash = hash * 4567 + TargetPath.GetHashCode();
            }

            return hash;
        }
    }
}
