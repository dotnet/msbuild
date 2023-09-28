// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    internal class LockFileCache
    {
        private IBuildEngine4 _buildEngine;
        private Logger _log;

        public LockFileCache(TaskBase task)
        {
            _buildEngine = task.BuildEngine4;
            _log = task.Log;
        }

        public LockFile GetLockFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new BuildErrorException(Strings.AssetsFileNotSet);
            }
            if (!Path.IsPathRooted(path))
            {
                throw new BuildErrorException(Strings.AssetsFilePathNotRooted, path);
            }

            string lockFileKey = GetTaskObjectKey(path);

            LockFile result;
            object existingLockFileTaskObject = _buildEngine?.GetRegisteredTaskObject(lockFileKey, RegisteredTaskObjectLifetime.Build);
            if (existingLockFileTaskObject == null)
            {
                result = LoadLockFile(path);

                _buildEngine?.RegisterTaskObject(lockFileKey, result, RegisteredTaskObjectLifetime.Build, true);
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
            // https://github.com/NuGet/Home/issues/6732
            //
            // LockFileUtilties.GetLockFile has odd error handling:
            //
            //   1. Exceptions creating TextReader from path (after up to 3 tries) will
            //      bubble out.
            //
            //   2. There's an up-front File.Exists that returns null without logging
            //      anything.
            //
            //   3. Any other exception whatsoever is logged by its Message property
            //      alone, and an empty, non-null lock file is returned.
            //
            // This wrapper will never return null or empty lock file and instead throw 
            // if the assets file is not found  or cannot be read for any other reason.

            LockFile lockFile;

            try
            {
                lockFile = LockFileUtilities.GetLockFile(
                    path,
                    new ThrowOnLockFileLoadError(_log));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Case 1
                throw new BuildErrorException(
                    string.Format(Strings.ErrorReadingAssetsFile, ex.Message),
                    ex);
            }

            if (lockFile == null)
            {
                // Case 2
                // NB: Cannot be moved to our own up-front File.Exists check or else there would be
                // a race where we still need to handle null for delete between our check and 
                // NuGet's.
                throw new BuildErrorException(Strings.AssetsFileNotFound, path);
            }

            return lockFile;
        }

        // Case 3
        // Force an exception on errors reading the lock file
        // Non-errors are not logged today, but push them to the build log in case they are in the future.
        private sealed class ThrowOnLockFileLoadError : LoggerBase
        {
            private Logger _log;

            public ThrowOnLockFileLoadError(Logger log)
            {
                _log = log;
            }

            public override void Log(ILogMessage message)
            {
                if (message.Level == LogLevel.Error)
                {
                    throw new BuildErrorException(
                        string.Format(Strings.ErrorReadingAssetsFile, message.Message));
                }

                _log.Log(
                    new Message(
                        level: message.Level == LogLevel.Warning ? MessageLevel.Warning : MessageLevel.NormalImportance,
                        code: message.Code == NuGetLogCode.Undefined ? default : message.Code.ToString(),
                        file: message.ProjectPath,
                        text: message.Message));
            }

            public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
            {
                Log(message);
                return Task.CompletedTask;
            }
        }
    }
}
