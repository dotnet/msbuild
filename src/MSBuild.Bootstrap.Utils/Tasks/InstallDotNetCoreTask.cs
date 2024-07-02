// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if RUNTIME_TYPE_NETCORE

using System.Diagnostics;
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
    public sealed class InstallDotNetCoreTask : Task
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

        /// <summary>
        /// Executes the task, downloading and running the .NET Core installation script.
        /// </summary>
        /// <returns>True if the task succeeded; otherwise, false.</returns>
        public override bool Execute()
        {
            ScriptExecutionSettings executionSettings = SetupScriptsExecutionSettings();
            if (!File.Exists(executionSettings.ScriptsFullPath))
            {
                AsyncTasks.Task.Run(() => DownloadScriptAsync(executionSettings.ScriptName, executionSettings.ScriptsFullPath)).GetAwaiter().GetResult();
            }

            MakeScriptExecutable(executionSettings.ScriptsFullPath);

            return RunScript(executionSettings);
        }

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
            if (IsWindows)
            {
                return;
            }

            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x {scriptPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            })
            {
                _ = process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errors = process.StandardError.ReadToEnd() ?? string.Empty;
                    Log.LogError($"Install-scripts can not be made executable due to the errors: {errors}.");
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
            if (Log.HasLoggedErrors)
            {
                return false;
            }

            using (Process process = new Process { StartInfo = executionSettings.StartInfo })
            {
                bool started = process.Start();
                if (started)
                {
                    string output = process.StandardOutput.ReadToEnd() ?? string.Empty;
                    Log.LogMessage($"Install-scripts output logs: {output}");

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string errors = process.StandardError.ReadToEnd() ?? string.Empty;
                        Log.LogError($"Install-scripts execution errors: {errors}");
                    }
                }
                else
                {
                    Log.LogError("Process for install-scripts execution has not started.");
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
            string executableName = IsWindows ? "powershell.exe" : "/bin/bash";
            string scriptPath = Path.Combine(DotNetInstallScriptRootPath, $"{ScriptName}.{scriptExtension}");
            string scriptArgs = IsWindows
                ? $"-NoProfile -ExecutionPolicy Bypass -File {scriptPath} -Version {Version} -InstallDir {InstallDir}"
                : $"{scriptPath} --version {Version} --install-dir {InstallDir}";

            var startInfo = new ProcessStartInfo
            {
                FileName = executableName,
                Arguments = scriptArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            return new ScriptExecutionSettings(executableName, startInfo, $"{ScriptName}.{scriptExtension}", scriptPath);
        }

        /// <summary>
        /// A private struct to hold settings for script execution.
        /// </summary>
        private struct ScriptExecutionSettings(string executableName, ProcessStartInfo startInfo, string scriptName, string scriptsFullPath)
        {
            public string ExecutableName { get; } = executableName;
            public ProcessStartInfo StartInfo { get; } = startInfo;
            public string ScriptName { get; } = scriptName;
            public string ScriptsFullPath { get; } = scriptsFullPath;
        }
    }
}

#endif
