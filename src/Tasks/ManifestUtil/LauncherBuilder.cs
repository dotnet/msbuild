// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;

#nullable disable

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

        internal TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public BuildResults Build(string filename, string outputPath)
        {
            AbsolutePath path = string.IsNullOrEmpty(outputPath) ? default : TaskEnvironment.GetAbsolutePath(outputPath);
            return Build(filename, path);
        }

        internal BuildResults Build(string filename, AbsolutePath outputPath)
        {
            _results = new BuildResults();

            try
            {
                if (filename == null)
                {
                    _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.InvalidInput"));
                    return _results;
                }

                if (String.IsNullOrEmpty(outputPath.Value))
                {
                    _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.NoOutputPath"));
                    return _results;
                }

                AbsolutePath launcherPath = string.IsNullOrEmpty(LauncherPath) ? default : TaskEnvironment.GetAbsolutePath(LauncherPath);
                string launcherPathForFileSystem = launcherPath.Value ?? LauncherPath;
                string launcherPathForMessages = launcherPath.OriginalValue ?? LauncherPath;
                string launcherFilename = Path.GetFileName(launcherPathForFileSystem);

                // Copy setup.bin to the output directory
                string strOutputExe = Path.Combine(outputPath.Value, launcherFilename);
                string strOutputExeForMessages = Path.Combine(outputPath.OriginalValue, launcherFilename);
                if (!CopyLauncherToOutputDirectory(launcherPathForFileSystem, launcherPathForMessages, strOutputExe, strOutputExeForMessages))
                {
                    // Appropriate messages should have been stuffed into the results already
                    return _results;
                }

                var resourceUpdater = new ResourceUpdater();
                resourceUpdater.AddStringResource(LAUNCHER_RESOURCE_TABLE, LAUNCHER_RESOURCENAME, filename);
                if (!resourceUpdater.UpdateResources(strOutputExe, _results, strOutputExeForMessages))
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

        private bool CopyLauncherToOutputDirectory(string launcherPathForFileSystem, string launcherPathForMessages, string strOutputExe, string strOutputExeForMessages)
        {
            if (!FileSystems.Default.FileExists(launcherPathForFileSystem))
            {
                _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.MissingLauncherExe", launcherPathForMessages));
                return false;
            }

            try
            {
                EnsureFolderExists(Path.GetDirectoryName(strOutputExe));
                File.Copy(launcherPathForFileSystem, strOutputExe, true);
                ClearReadOnlyAttribute(strOutputExe);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                _results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateLauncher.CopyError", launcherPathForMessages, strOutputExeForMessages, ex.Message));
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
