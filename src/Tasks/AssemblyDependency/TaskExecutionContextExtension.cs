// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    public static class TaskExecutionContextExtension
    {
        /// <summary>
        /// Absolutize the given path with the startup directory.
        /// </summary>
        /// <param name="taskExecutionContext"></param>
        /// <param name="path">Relative or absolute path.</param>
        /// <returns></returns>
        public static string GetFullPath(this TaskExecutionContext taskExecutionContext,  string path)
        {
            if (String.IsNullOrEmpty(taskExecutionContext.StartupDirectory) || String.IsNullOrEmpty(path))
            {
                return path;
            }

            try
            {
                // Path.GetFullPath is using in order to eliminate possible "./" and "../" in the resulted path.
                // However, if the combined path consists of different path separators (both windows and unix style),
                // then the behavior of Path.GetFullPath differs in windows and unix systems, as in Windows both Windows and Unix style separators works and in Unix - not.
                // Windows' function eleminates the internal "./" and "../", Unix's function does not. We are using FixFilePath to remove windows-style separators when on unix machine.
                return Path.GetFullPath(Path.Combine(taskExecutionContext.StartupDirectory, FileUtilities.FixFilePath(path)));
            }
            catch { }

            return path;
        }
    }
}
