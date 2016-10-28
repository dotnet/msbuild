// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions
{
    internal abstract class DirectoryInfoBase : FileSystemInfoBase
    {
        public abstract IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos();

        public abstract DirectoryInfoBase GetDirectory(string path);

        public abstract FileInfoBase GetFile(string path);
    }
}