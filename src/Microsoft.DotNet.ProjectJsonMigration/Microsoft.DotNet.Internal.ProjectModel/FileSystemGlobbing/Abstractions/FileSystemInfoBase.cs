// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions
{
    internal abstract class FileSystemInfoBase
    {
        public abstract string Name { get; }

        public abstract string FullName { get; }

        public abstract DirectoryInfoBase ParentDirectory { get; }
    }
}