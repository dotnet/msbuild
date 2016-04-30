// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal
{
    public interface IPatternContext
    {
        void Declare(Action<IPathSegment, bool> onDeclare);

        bool Test(DirectoryInfoBase directory);

        PatternTestResult Test(FileInfoBase file);

        void PushDirectory(DirectoryInfoBase directory);

        void PopDirectory();
    }
}
