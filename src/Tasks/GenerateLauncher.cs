// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a bootstrapper for ClickOnce deployment projects.
    /// </summary>
    public sealed class GenerateLauncher : TaskExtension
    {
        private const string LAUNCHER_EXE = "Launcher.exe";
        private const string ENGINE_PATH = "Engine"; // relative to ClickOnce bootstrapper path

        #region Properties

        public ITaskItem EntryPoint { get; set; }

        public string LauncherPath { get; set; }

        public string OutputPath { get; set; }

        public string VisualStudioVersion { get; set; }

        public string AssemblyName { get; set; }

        [Output]
        public ITaskItem OutputEntryPoint { get; set; }
        #endregion

        public override bool Execute()
        {
            if (LauncherPath == null)
            {
                // Launcher lives next to ClickOnce bootstrapper.
                // GetDefaultPath obtains the root ClickOnce boostrapper path.
                LauncherPath = Path.Combine(
                    Microsoft.Build.Tasks.Deployment.Bootstrapper.Util.GetDefaultPath(VisualStudioVersion),
                    ENGINE_PATH,
                    LAUNCHER_EXE);
            }

            if (EntryPoint == null)
            {
                Log.LogErrorWithCodeFromResources("GenerateLauncher.InvalidInput");
                return false;
            }

            var launcherBuilder = new LauncherBuilder(LauncherPath);
            string entryPointFileName = Path.GetFileName(EntryPoint.ItemSpec);
            //
            // If the EntryPoint specified is apphost.exe or singlefilehost.exe, we need to replace the EntryPoint
            // with the AssemblyName instead since apphost.exe/singlefilehost.exe is an intermediate file for
            // for final published {assemblyname}.exe.
            //
            if ((entryPointFileName.Equals(Constants.AppHostExe, StringComparison.InvariantCultureIgnoreCase) || 
                entryPointFileName.Equals(Constants.SingleFileHostExe, StringComparison.InvariantCultureIgnoreCase)) &&
                !String.IsNullOrEmpty(AssemblyName))
            {
                entryPointFileName = AssemblyName;
            }
            BuildResults results = launcherBuilder.Build(entryPointFileName, OutputPath);

            BuildMessage[] messages = results.Messages;
            if (messages != null)
            {
                foreach (BuildMessage message in messages)
                {
                    switch (message.Severity)
                    {
                        case BuildMessageSeverity.Error:
                            Log.LogError(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                            break;
                        case BuildMessageSeverity.Warning:
                            Log.LogWarning(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                            break;
                        case BuildMessageSeverity.Info:
                            Log.LogMessage(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                            continue;
                    }
                }
            }

            OutputEntryPoint = new TaskItem(Path.Combine(Path.GetDirectoryName(EntryPoint.ItemSpec), results.KeyFile));
            OutputEntryPoint.SetMetadata(ItemMetadataNames.targetPath, results.KeyFile);

            return !Log.HasLoggedErrors;
        }
    }
}
