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
                // TODO: Does GetFullPath access the file system? If so, find a way to remove internal ../ and ./ without it.
                // Use URI, perhaps?
                return Path.GetFullPath(Path.Combine(StartupDirectory, path));
            }
            catch { }

            return path;
        }
    }

    // TODO: move to own file
    public interface IConcurrentTask
    {
        void ConfigureForConcurrentExecution(TaskExecutionContext executionContext);
    }
}
