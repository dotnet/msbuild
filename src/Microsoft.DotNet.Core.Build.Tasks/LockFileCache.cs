// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Tasks
{
    internal class LockFileCache
    {
        public static LockFileCache Instance { get; } = new LockFileCache();

        private ConcurrentDictionary<string, LockFile> _cache = new ConcurrentDictionary<string, LockFile>();

        private LockFileCache()
        {
        }

        public LockFile GetLockFile(string path)
        {
            LockFile result;
            if (!_cache.TryGetValue(path, out result))
            {
                result = LoadLockFile(path);
                _cache[path] = result;
            }
            return result;
        }

        private LockFile LoadLockFile(string path)
        {
            // TODO adapt task logger to Nuget Logger
            return LockFileUtilities.GetLockFile(path, NullLogger.Instance);
        }
    }
}
