// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions
{
    internal class FileInfoWrapper : FileInfoBase
    {
        private FileInfo _fileInfo;

        public FileInfoWrapper(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
        }

        public override string Name
        {
            get { return _fileInfo.Name; }
        }

        public override string FullName
        {
            get { return _fileInfo.FullName; }
        }

        public override DirectoryInfoBase ParentDirectory
        {
            get { return new DirectoryInfoWrapper(_fileInfo.Directory); }
        }
    }
}