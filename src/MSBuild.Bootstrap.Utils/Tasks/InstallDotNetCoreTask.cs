// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if RUNTIME_TYPE_NETCORE

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Bootstrap.Utils.Tasks
{
    public sealed class InstallDotNetCoreTask : Task
    {
        private const string ScriptName = "dotnet-install";
        private const string DotNetInstallBaseUrl = "https://dot.net/v1/";

        public InstallDotNetCoreTask()
        {
            InstallDir = string.Empty;
            DotNetInstallScriptRootPath = string.Empty;
            Version = string.Empty;
        }

        [Required]
        public string InstallDir { get; set; }

        [Required]
        public string DotNetInstallScriptRootPath { get; set; }

        [Required]
        public string Version { get; set; }

        public override bool Execute()
        {
            ScriptExecutionSettings executionSettings = SetupScriptsExecutionSettings();
            if (!File.Exists(executionSettings.ScriptsFullPath))
            {
                DownloadScript(executionSettings.ScriptName, executionSettings.ScriptsFullPath);
            }

            return RunScript(executionSettings);
        }

        private void DownloadScript(string scriptName, string scriptPath)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync($"{DotNetInstallBaseUrl}{scriptName}").Result;
                response.EnsureSuccessStatusCode();

                string scriptContent = response.Content.ReadAsStringAsync().Result;
                File.WriteAllText(scriptPath, scriptContent);
            }
        }

        private bool RunScript(ScriptExecutionSettings executionSettings)
        {
            using (Process process = new Process { StartInfo = executionSettings.StartInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                Log.LogMessage(output);

                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log.LogError("Errors: " + errors);
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }

        private bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private struct ScriptExecutionSettings(string executableName, ProcessStartInfo startInfo, string scriptName, string scriptsFullPath)
        {
            public string ExecutableName { get; } = executableName;

            public ProcessStartInfo StartInfo { get; } = startInfo;

            public string ScriptName { get; } = scriptName;

            public string ScriptsFullPath { get; } = scriptsFullPath;
        }

        private ScriptExecutionSettings SetupScriptsExecutionSettings()
        {
            string scriptExtension = IsWindows ? "ps1" : "sh";
            string executableName = IsWindows ? "powershell.exe" : "/bin/bash";
            string scriptPath = Path.Combine(DotNetInstallScriptRootPath, $"{ScriptName}.{scriptExtension}");
            string scriptArgs = IsWindows
                ? $"-NoProfile -ExecutionPolicy Bypass -File {scriptPath} -Version {Version} -InstallDir {InstallDir}"
                : $"--version {Version} --install-dir {InstallDir}";

            var startInfo = new ProcessStartInfo
            {
                FileName = IsWindows ? executableName : "chmod",
                Arguments = IsWindows ? scriptArgs : $"+x {scriptPath} {scriptArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            return new ScriptExecutionSettings(executableName, startInfo, $"{ScriptName}.{scriptExtension}", scriptPath);
        }
    }
}

#endif
