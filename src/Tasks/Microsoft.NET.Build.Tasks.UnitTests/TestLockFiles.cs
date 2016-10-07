// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using NuGet.ProjectModel;
using System.IO;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal static class TestLockFiles
    {
        public static LockFile GetLockFile(string lockFilePrefix)
        {
            string filePath = Path.Combine("LockFiles", $"{lockFilePrefix}.project.lock.json");

            return LockFileUtilities.GetLockFile(filePath, NullLogger.Instance);
        }

        public static LockFile CreateLockFile(string contents, string path = "path/to/project.lock.json")
        {
            return new LockFileFormat().Parse(contents, path);
        }
    }
}