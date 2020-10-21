// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Adds Launcher and updates its resource
    /// </summary>
    public class LauncherBuilder
    {
        private const string LAUNCHER_RESOURCENAME = "FILENAME";
        private const int LAUNCHER_RESOURCE_TABLE = 50;

        private BuildResults _results;

        public LauncherBuilder(string launcherPath)
        {
            LauncherPath = launcherPath;
        }

        /// <summary>
        /// Specifies the location of the required Launcher files.
        /// </summary>
        /// <value>Path to Launcher files.</value>
        public string LauncherPath { get; set; }

        public BuildResults Build(string filename, string outputPath)
        {
            string launcherFilename = Path.GetFileName(LauncherPath);

            _results = new BuildResults();

            try
            {
                if (filename == null)
                {
                    _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.InvalidInput"));
                    return _results;
                }

                if (String.IsNullOrEmpty(outputPath))
                {
                    _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.NoOutputPath"));
                    return _results;
                }

                // Copy setup.bin to the output directory
                string strOutputExe = System.IO.Path.Combine(outputPath, launcherFilename);
                if (!CopyLauncherToOutputDirectory(strOutputExe))
                {
                    // Appropriate messages should have been stuffed into the results already
                    return _results;
                }

                var resourceUpdater = new ResourceUpdater();
                resourceUpdater.AddStringResource(LAUNCHER_RESOURCE_TABLE, LAUNCHER_RESOURCENAME, filename);
                if (!resourceUpdater.UpdateResources(strOutputExe, _results))
                {
                    return _results;
                }

                _results.SetKeyFile(launcherFilename);
                _results.BuildSucceeded();
            }
            catch (Exception ex)
            {
                _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.General", ex.Message));
            }

            return _results;
        }

        private bool CopyLauncherToOutputDirectory(string strOutputExe)
        {
            if (!FileSystems.Default.FileExists(LauncherPath))
            {
                _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.MissingLauncherExe", LauncherPath));
                return false;
            }

            try
            {
                EnsureFolderExists(Path.GetDirectoryName(strOutputExe));
                File.Copy(LauncherPath, strOutputExe, true);
                ClearReadOnlyAttribute(strOutputExe);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.CopyError", LauncherPath, strOutputExe, ex.Message));
                return false;
            }

            return true;
        }

        private static void EnsureFolderExists(string strFolderPath)
        {
            if (!FileSystems.Default.DirectoryExists(strFolderPath))
            {
                Directory.CreateDirectory(strFolderPath);
            }
        }

        private static void ClearReadOnlyAttribute(string strFileName)
        {
            FileAttributes attribs = File.GetAttributes(strFileName);
            if ((attribs & FileAttributes.ReadOnly) != 0)
            {
                attribs &= (~FileAttributes.ReadOnly);
                File.SetAttributes(strFileName, attribs);
            }
        }
    }
}
