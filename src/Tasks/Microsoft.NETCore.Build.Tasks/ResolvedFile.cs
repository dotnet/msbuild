// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.NETCore.Build.Tasks
{
    public class ResolvedFile
    {
        public string SourcePath { get; }

        public string DestinationSubDirectory { get; }

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

        public ResolvedFile(string sourcePath, string destinationSubDirectory)
        {
            SourcePath = sourcePath;
            DestinationSubDirectory = destinationSubDirectory;
        }

        public override bool Equals(object obj)
        {
            ResolvedFile other = obj as ResolvedFile;
            return other != null &&
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
