// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using AsyncTasks = System.Threading.Tasks;

namespace MSBuild.Bootstrap.Utils.Tasks
{
    /// <summary>
    /// This task is designed to automate the installation of .NET Core SDK.
    /// It downloads the appropriate installation script and executes it to install the specified version of .NET Core SDK.
    /// </summary>
    public sealed class InstallDotNetCoreTask : ToolTask
    {
        private const string ScriptName = "dotnet-install";

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallDotNetCoreTask"/> class.
        /// </summary>
        public InstallDotNetCoreTask()
        {
            InstallDir = string.Empty;
            DotNetInstallScriptRootPath = string.Empty;
            Version = string.Empty;
        }

        /// <summary>
        /// Gets or sets the directory where the .NET Core SDK should be installed. This property is required.
        /// </summary>
        [Required]
        public string InstallDir { get; set; }

        /// <summary>
        /// Gets or sets the root path where the .NET Core installation script is located. This property is required.
        /// </summary>
        [Required]
        public string DotNetInstallScriptRootPath { get; set; }

        /// <summary>
        /// Gets or sets the version of the .NET Core SDK to be installed. This property is required.
        /// </summary>
        [Required]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the base URL for downloading the .NET Core installation script. The default value is "https://dot.net/v1/".
        /// </summary>
        public string DotNetInstallBaseUrl { get; set; } = "https://dot.net/v1/";

        private bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        protected override string ToolName => IsWindows ? "powershell.exe" : "/bin/bash";

        /// <summary>
        /// Executes the task, downloading and running the .NET Core installation script.
        /// </summary>
        /// <returns>True if the task succeeded; otherwise, false.</returns>
        public override bool Execute()
        {
            if (Directory.Exists(Path.Combine(InstallDir, "sdk", Version)))
            {
                // no need to download sdk again, it exists locally
                return true;
            }

            ScriptExecutionSettings executionSettings = SetupScriptsExecutionSettings();
            if (!File.Exists(executionSettings.ScriptsFullPath))
            {
                AsyncTasks.Task.Run(() => DownloadScriptAsync(executionSettings.ScriptName, executionSettings.ScriptsFullPath)).GetAwaiter().GetResult();
            }

            MakeScriptExecutable(executionSettings.ScriptsFullPath);

            return RunScript(executionSettings);
        }

        protected override string GenerateFullPathToTool() => ToolName;

        // Do not use the normal parse-for-canonical-errors mechanism since install-scripts can emit nonfatal curl errors that match that pattern.
        // Instead, log everything as a message and rely on the final success/failure return.
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance) => Log.LogMessage(messageImportance, singleLine);

        /// <summary>
        /// Downloads the .NET Core installation script asynchronously from the specified URL.
        /// </summary>
        /// <param name="scriptName">The name of the script to download.</param>
        /// <param name="scriptPath">The path where the script will be saved.</param>
        private async AsyncTasks.Task DownloadScriptAsync(string scriptName, string scriptPath)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync($"{DotNetInstallBaseUrl}{scriptName}").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string scriptContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(scriptContent))
                    {
                        File.WriteAllText(scriptPath, scriptContent);
                    }
                }
                else
                {
                    Log.LogError($"Install-scripts download from {DotNetInstallBaseUrl} error. Status code: {response.StatusCode}.");
                }
            }
        }

        /// <summary>
        /// Makes the installation script executable on non-Windows platforms.
        /// </summary>
        /// <param name="scriptPath">The path of the script to make executable.</param>
        private void MakeScriptExecutable(string scriptPath)
        {
            if (!IsWindows)
            {
                int exitCode = ExecuteTool("/bin/chmod", string.Empty, $"+x {scriptPath}");
                if (exitCode != 0)
                {
                    Log.LogError($"Install-scripts can not be made executable due to the errors reported above.");
                }
            }
        }

        /// <summary>
        /// Runs the .NET Core installation script with the specified settings.
        /// </summary>
        /// <param name="executionSettings">The settings required for script execution.</param>
        /// <returns>True if the script executed successfully; otherwise, false.</returns>
        private bool RunScript(ScriptExecutionSettings executionSettings)
        {
            if (!Log.HasLoggedErrors)
            {
                int exitCode = ExecuteTool(ToolName, string.Empty, executionSettings.ExecutableArgs);

                if (exitCode != 0)
                {
                    Log.LogError($"Install-scripts was not executed successfully.");
                }
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Sets up the settings required for executing the .NET Core installation script.
        /// </summary>
        /// <returns>The settings required for script execution.</returns>
        private ScriptExecutionSettings SetupScriptsExecutionSettings()
        {
            string scriptExtension = IsWindows ? "ps1" : "sh";
            string scriptPath = Path.Combine(DotNetInstallScriptRootPath, $"{ScriptName}.{scriptExtension}");
            string scriptArgs = IsWindows
                ? $"-NoProfile -ExecutionPolicy Bypass -File {scriptPath} -Version {Version} -InstallDir {InstallDir}"
                : $"{scriptPath} --version {Version} --install-dir {InstallDir}";

            return new ScriptExecutionSettings($"{ScriptName}.{scriptExtension}", scriptPath, scriptArgs);
        }

        /// <summary>
        /// A private struct to hold settings for script execution.
        /// </summary>
        private readonly struct ScriptExecutionSettings(string scriptName, string scriptsFullPath, string executableArgs)
        {
            public string ScriptName { get; } = scriptName;

            public string ScriptsFullPath { get; } = scriptsFullPath;

            public string ExecutableArgs { get; } = executableArgs;
        }
    }
}
