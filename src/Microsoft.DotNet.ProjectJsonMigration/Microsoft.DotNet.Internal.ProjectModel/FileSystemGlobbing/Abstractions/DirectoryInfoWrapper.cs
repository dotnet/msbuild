// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions
{
    internal class DirectoryInfoWrapper : DirectoryInfoBase
    {
        private readonly DirectoryInfo _directoryInfo;
        private readonly bool _isParentPath;

        public DirectoryInfoWrapper(DirectoryInfo directoryInfo, bool isParentPath = false)
        {
            _directoryInfo = directoryInfo;
            _isParentPath = isParentPath;
        }

        public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            if (_directoryInfo.Exists)
            {
                foreach (var fileSystemInfo in _directoryInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                {
                    var directoryInfo = fileSystemInfo as DirectoryInfo;
                    if (directoryInfo != null)
                    {
                        yield return new DirectoryInfoWrapper(directoryInfo);
                    }
                    else
                    {
                        yield return new FileInfoWrapper((FileInfo)fileSystemInfo);
                    }
                }
            }
        }

        public override DirectoryInfoBase GetDirectory(string name)
        {
            var isParentPath = string.Equals(name, "..", StringComparison.Ordinal);

            if (isParentPath)
            {
                return new DirectoryInfoWrapper(new DirectoryInfo(Path.Combine(_directoryInfo.FullName, name)), isParentPath);
            }
            else
            {
                var dirs = _directoryInfo.GetDirectories(name);

                if (dirs.Length == 1)
                {
                    return new DirectoryInfoWrapper(dirs[0], isParentPath);
                }
                else if (dirs.Length == 0)
                {
                    return null;
                }
                else
                {
                    // This shouldn't happen. The parameter name isn't supposed to contain wild card.
                    throw new InvalidOperationException(
                        string.Format("More than one sub directories are found under {0} with name {1}.", _directoryInfo.FullName, name));
                }
            }
        }

        public override FileInfoBase GetFile(string name)
        {
            return new FileInfoWrapper(new FileInfo(Path.Combine(_directoryInfo.FullName, name)));
        }

        public override string Name
        {
            get { return _isParentPath ? ".." : _directoryInfo.Name; }
        }

        public override string FullName
        {
            get { return _directoryInfo.FullName; }
        }

        public override DirectoryInfoBase ParentDirectory
        {
            get { return new DirectoryInfoWrapper(_directoryInfo.Parent); }
        }
    }
}
