// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Core.Build.Tasks.UnitTests
{
    internal static class TestLockFiles
    {
        public static LockFile GetLockFile(string lockFilePrefix)
        {
            string filePath = Path.Combine("LockFiles", $"{lockFilePrefix}.project.lock.json");

            return LockFileUtilities.GetLockFile(filePath, NullLogger.Instance);
        }
    }
}