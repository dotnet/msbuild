// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Microsoft.Build.Framework
{
    public class TaskExecutionContext
    {
        public string StartupDirectory { get; }
        public Dictionary<string, string> BuildProcessEnvironment { get; }
        public CultureInfo Culture { get; }
        public CultureInfo UICulture { get; }

        public TaskExecutionContext()
        {
            StartupDirectory = null;
            BuildProcessEnvironment = null;
            Culture = null;
            UICulture = null;
        }

        public TaskExecutionContext(string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            StartupDirectory = startupDirectory;
            BuildProcessEnvironment = buildProcessEnvironment;
            Culture = culture;
            UICulture = uiCulture;
        }

        /// <summary>
        /// Absolutize the given path with the startup directory.
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        /// <returns></returns>
        public string GetFullPath(string path)
        {
            if (String.IsNullOrEmpty(StartupDirectory) || String.IsNullOrEmpty(path))
            {
                return path;
            }

            try
            {
                // Path.GetFullPath is using in order to eliminate possible "./" and "../" in the resulted path.
                // TODO: Check what version of Path.GetFullPath we are using. Does it use IO operations in file system? If yes, consider other options for dealing with "./" and "../".
                // However, if the combined path consists of different path separators (both windows and unix style),
                // then the behavior of Path.GetFullPath differs in windows and unix systems. Windows' function eleminates the internal "./" and "../"
                // and Unix's function does not. We are using FixFilePath to remove windows-style separators when on unix machine.
                return Path.GetFullPath(Path.Combine(StartupDirectory, FixFilePath(path)));
            }
            catch { }

            return path;
        }

        // This function is a duplicate of FileUtilities.FixFilePath.
        // The reason for code duplication is that we do not want to bring new dependencies to Microsoft.Build.Framework.
        /// <summary>
        /// Replaces Windows-style path separators with Unix-style path separators, when performed on unix.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');//.Replace("//", "/");
        }
    }

    // TODO: move to own file
    public interface IConcurrentTask
    {
        void ConfigureForConcurrentExecution(TaskExecutionContext executionContext);
    }
}
