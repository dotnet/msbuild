// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.TestUtility
{
    internal class MockFileInfo : FileInfoBase
    {
        public MockFileInfo(
            FileSystemOperationRecorder recorder,
            DirectoryInfoBase parentDirectory,
            string fullName,
            string name)
        {
            Recorder = recorder;
            FullName = fullName;
            Name = name;
        }

        public FileSystemOperationRecorder Recorder { get; }

        public override DirectoryInfoBase ParentDirectory { get; }

        public override string FullName { get; }

        public override string Name { get; }
    }
}