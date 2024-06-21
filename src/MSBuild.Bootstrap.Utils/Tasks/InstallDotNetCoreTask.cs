// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;

namespace MSBuild.Bootstrap.Utils.Tasks
{
    public sealed class InstallDotNetCoreTask : TaskExtension
    {
        private const string DotNetInstallBaseUrl = "https://dot.net/v1/";

        public InstallDotNetCoreTask()
        {
            InstallDir = string.Empty;
            DotNetInstallScript = string.Empty;
            Channel = string.Empty;
        }

        [Required]
        public string InstallDir { get; set; }

        [Required]
        public string DotNetInstallScript { get; set; }

        public string Channel { get; set; }

        public override bool Execute()
        {
            string scriptName = GetScriptName();
            string scriptPath = Path.Combine(DotNetInstallScript, scriptName);

            if (!File.Exists(scriptPath))
            {
                DownloadScript(scriptName, scriptPath);
            }

            string scriptArgs = GetScriptArgs();
            Log.LogMessage(MessageImportance.Low, $"Executing: {scriptPath} {scriptArgs}");

            if (!NativeMethods.IsWindows)
            {
                MakeScriptExecutable(scriptPath);
            }

            return RunScript(scriptPath, scriptArgs);
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

        private void MakeScriptExecutable(string scriptPath)
        {
            using (Process chmodProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x {scriptPath}",
                    UseShellExecute = false
                },
            })
            {
                chmodProcess.Start();
                chmodProcess.WaitForExit();
            }
        }

        private bool RunScript(string scriptPath, string scriptArgs)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = GetProcessName(),
                Arguments = GetProcessArguments(scriptPath, scriptArgs),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
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

                    Log.LogError("dotnet-install failed");
                }
            }

            return !Log.HasLoggedErrors;
        }

        private string GetScriptName() => NativeMethodsShared.IsWindows ? "dotnet-install.ps1" : "dotnet-install.sh";

        private string GetProcessName() => NativeMethodsShared.IsWindows ? "powershell.exe" : @"/bin/bash";

        private string GetProcessArguments(string scriptPath, string scriptArgs) => NativeMethodsShared.IsWindows
            ? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {scriptArgs}"
            : $"{scriptPath} {scriptArgs}";

        private string GetScriptArgs() => NativeMethodsShared.IsWindows
            ? $"{(string.IsNullOrEmpty(Channel) ? "-Quality preview" : $"-Channel {Channel}")} -InstallDir {InstallDir}"
            : $"{(string.IsNullOrEmpty(Channel) ? "--quality preview" : $"--channel {Channel}")} --install-dir {InstallDir}";
    }
}
