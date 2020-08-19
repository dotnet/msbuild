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

        [Output]
        public ITaskItem OutputEntryPoint { get; set; }

        [Output]
        public string FrameworkName { get; set; }

        [Output]
        public string FrameworkVersion { get; set; }
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
                return false;
            }

            // Get Framework name and version.
            // Launcher-based manifest generation has to use Framework elements that match Launcher identity.
            Assembly a = Assembly.UnsafeLoadFrom(LauncherPath);
            var targetFrameworkAttribute = a.GetCustomAttribute<TargetFrameworkAttribute>();
            if (targetFrameworkAttribute != null)
            {
                FrameworkName = targetFrameworkAttribute.FrameworkName;
                string[] split = FrameworkName.Split(new string[] { "Version=" }, StringSplitOptions.None);
                FrameworkVersion = split.Length > 1 ? split[split.Length - 1] : string.Empty;
            }

            var launcherBuilder = new LauncherBuilder(LauncherPath);
            BuildResults results = launcherBuilder.Build(Path.GetFileName(EntryPoint.ItemSpec), OutputPath);

            BuildMessage[] messages = results.Messages;
            if (messages != null)
            {
                foreach (BuildMessage message in messages)
                {
                    if (message.Severity == BuildMessageSeverity.Error)
                    {
                        Log.LogError(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                    }
                    else if (message.Severity == BuildMessageSeverity.Warning)
                    {
                        Log.LogWarning(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                    }
                }
            }

            OutputEntryPoint = new TaskItem(Path.Combine(Path.GetDirectoryName(EntryPoint.ItemSpec), results.KeyFile));
            OutputEntryPoint.SetMetadata(ItemMetadataNames.targetPath, results.KeyFile);

            return results.Succeeded;
        }
    }
}
