// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    internal class LockFileCache
    {
        private IBuildEngine4 _buildEngine;
        
        public LockFileCache(IBuildEngine4 buildEngine)
        {
            _buildEngine = buildEngine;
        }

        public LockFile GetLockFile(string path)
        {
            string lockFileKey = GetTaskObjectKey(path);

            LockFile result;
            object existingLockFileTaskObject = _buildEngine.GetRegisteredTaskObject(lockFileKey, RegisteredTaskObjectLifetime.Build);
            if (existingLockFileTaskObject == null)
            {
                result = LoadLockFile(path);

                _buildEngine.RegisterTaskObject(lockFileKey, result, RegisteredTaskObjectLifetime.Build, true);
            }
            else
            {
                result = (LockFile)existingLockFileTaskObject;
            }

            return result;
        }

        private static string GetTaskObjectKey(string lockFilePath)
        {
            return $"{nameof(LockFileCache)}:{lockFilePath}";
        }

        private LockFile LoadLockFile(string path)
        {
            // TODO adapt task logger to Nuget Logger
            return LockFileUtilities.GetLockFile(path, NullLogger.Instance);
        }
    }
}
