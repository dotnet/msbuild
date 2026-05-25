// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;
using Constants = Microsoft.Build.Tasks.Deployment.ManifestUtilities.Constants;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a bootstrapper for ClickOnce deployment projects.
    /// </summary>
    [MSBuildMultiThreadableTask]
    [SupportedOSPlatform("windows")]
    public sealed class GenerateLauncher : TaskExtension, IMultiThreadableTask
    {
        private const string LAUNCHER_EXE = "Launcher.exe";
        private const string ENGINE_PATH = "Engine"; // relative to ClickOnce bootstrapper path

        #region Properties
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
        public ITaskItem EntryPoint { get; set; }

        public string LauncherPath { get; set; }

        public string OutputPath { get; set; }

        public string VisualStudioVersion { get; set; }

        public string AssemblyName { get; set; }

        [Output]
        public ITaskItem OutputEntryPoint { get; set; }
        #endregion

        // MSBuildTask0005 (transitive unsafe API) is reported here by the call-graph analyzer
        // because ResourceUpdater.UpdateResources contains a File.OpenRead call. That branch
        // only executes when ResourceUpdater._fileResources is non-empty, which happens only
        // via AddFileResource — and AddFileResource is called exclusively from
        // BootstrapperBuilder, never from LauncherBuilder (which uses AddStringResource only).
        // All other I/O reachable from this task — File.Copy, File.Get/SetAttributes,
        // Directory.CreateDirectory, FileSystems.Default.FileExists/DirectoryExists — receives
        // an AbsolutePath.Value, and Util.GetDefaultPath is called with an explicit project-
        // scoped fallback (TaskEnvironment.ProjectDirectory.Value) instead of
        // Directory.GetCurrentDirectory(). The warning is left unsuppressed so the analyzer
        // can be improved to track data-flow reachability; see analyzer issue (to be filed).
        public override bool Execute()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Log.LogErrorWithCodeFromResources("General.TaskRequiresWindows", nameof(GenerateLauncher));
                return false;
            }

            if (LauncherPath == null)
            {
                // Launcher lives next to ClickOnce bootstrapper.
                // Use the TaskEnvironment overload of GetDefaultPath so the fallback is the
                // project directory rather than the process-wide current directory.
                LauncherPath = Path.Combine(
                    Deployment.Bootstrapper.Util.GetDefaultPath(VisualStudioVersion, TaskEnvironment),
                    ENGINE_PATH,
                    LAUNCHER_EXE);
            }
            if (EntryPoint == null)
            {
                Log.LogErrorWithCodeFromResources("GenerateLauncher.InvalidInput");
                return false;
            }

            AbsolutePath outputPath = string.IsNullOrEmpty(OutputPath) ? default : TaskEnvironment.GetAbsolutePath(OutputPath);

            var launcherBuilder = new LauncherBuilder(LauncherPath, TaskEnvironment);
            string entryPointFileName = Path.GetFileName(EntryPoint.ItemSpec);

            // If the EntryPoint specified is apphost.exe or singlefilehost.exe, we need to replace the EntryPoint
            // with the AssemblyName instead since apphost.exe/singlefilehost.exe is an intermediate file for
            // for final published {assemblyname}.exe.
            if ((entryPointFileName.Equals(Constants.AppHostExe, StringComparison.InvariantCultureIgnoreCase) ||
                entryPointFileName.Equals(Constants.SingleFileHostExe, StringComparison.InvariantCultureIgnoreCase)) &&
                !string.IsNullOrEmpty(AssemblyName))
            {
                entryPointFileName = AssemblyName;
            }

            BuildResults results = launcherBuilder.Build(entryPointFileName, outputPath);

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
            string outputEntryPoint = Path.Combine(Path.GetDirectoryName(EntryPoint.ItemSpec), results.KeyFile);
            OutputEntryPoint = new TaskItem(outputEntryPoint);
            OutputEntryPoint.SetMetadata(ItemMetadataNames.targetPath, results.KeyFile);

            return !Log.HasLoggedErrors;
        }
    }
}
