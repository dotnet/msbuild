// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
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
            if (!Path.IsPathRooted(path))
            {
                throw new BuildErrorException("Assets file path '{0}' is not rooted. Only full paths are supported.", path);
            }

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
            if (!File.Exists(path))
            {
                throw new BuildErrorException("Assets file '{0}' not found. Run a NuGet package restore to generate this file.", path);
            }

            // TODO - https://github.com/dotnet/sdk/issues/18 adapt task logger to Nuget Logger
            return LockFileUtilities.GetLockFile(path, NullLogger.Instance);
        }
    }
}
