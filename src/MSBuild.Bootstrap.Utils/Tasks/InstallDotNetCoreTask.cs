// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        private const int MaxDownloadAttempts = 3;
        private const string MainBuildsTableUrl = "https://raw.githubusercontent.com/dotnet/dotnet/main/docs/builds-table.md";

        private static readonly Regex s_channelPattern = new(
            @"\A[0-9]+\.[0-9]+(?:\.[0-9]+[xX][xX])?(?:-[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*)?\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex s_mainSdkLinkPattern = new(
            @"^\[[^\]\r\n]+-main\]:\s+https://aka\.ms/dotnet/(?<channel>[^/\s]+)/daily/dotnet-sdk-[^/\s]+\.(?:zip|tar\.gz|exe|pkg)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex s_installedVersionPattern = new(
            @"(?:Installed version is (?<version>\S+)|SDK with version '(?<version>[^']+)' is already installed\.)",
            RegexOptions.CultureInvariant);
        private static readonly Regex s_sdkVersionPattern = new(
            @"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*)?(?:\+[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*)?\z",
            RegexOptions.CultureInvariant);

        private string? _reportedSdkVersion;

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
        /// Gets or sets the repo-local root path where the .NET Core installation script is downloaded to and run from.
        /// This must be a writable directory; it is intentionally independent of where the SDK itself is installed so the
        /// script is never written into a machine-global location such as "C:\Program Files\dotnet\". This property is required.
        /// </summary>
        [Required]
        public string DotNetInstallScriptRootPath { get; set; }

        /// <summary>
        /// Gets or sets the version of the .NET Core SDK to be installed. This property is required.
        /// </summary>
        [Required]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets whether to install the latest daily SDK instead of the specified version.
        /// </summary>
        public bool UseDailySdk { get; set; }

        /// <summary>
        /// Gets or sets the daily SDK channel. When empty, discover the published channel for dotnet/dotnet main.
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Gets the exact version of the installed SDK.
        /// </summary>
        [Output]
        public string InstalledSdkVersion { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the base URL for downloading the .NET Core installation script. The default value is "https://dot.net/v1/".
        /// </summary>
        public string DotNetInstallBaseUrl { get; set; } = "https://dot.net/v1/";

        internal HttpMessageHandler? HttpMessageHandler { get; set; }

        private bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        protected override string ToolName => IsWindows ? "powershell.exe" : "/bin/bash";

        /// <summary>
        /// Executes the task, downloading and running the .NET Core installation script.
        /// </summary>
        /// <returns>True if the task succeeded; otherwise, false.</returns>
        public override bool Execute()
        {
            InstalledSdkVersion = string.Empty;
            _reportedSdkVersion = null;

            if (!UseDailySdk && Directory.Exists(Path.Combine(InstallDir, "sdk", Version)))
            {
                // no need to download sdk again, it exists locally
                InstalledSdkVersion = Version;
                return true;
            }

            string? channel = null;
            if (UseDailySdk)
            {
                channel = string.IsNullOrEmpty(Channel)
                    ? AsyncTasks.Task.Run(DiscoverMainSdkChannelAsync).GetAwaiter().GetResult()
                    : Channel;

                if (channel is null)
                {
                    return false;
                }

                if (!s_channelPattern.IsMatch(channel))
                {
                    Log.LogError($"Daily bootstrap SDK channel '{channel}' is invalid. Set BootstrapSdkChannel to a numeric daily channel such as '11.0.1xx'.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"Acquiring the latest daily bootstrap SDK from channel '{channel}'.");
            }

            ScriptExecutionSettings executionSettings = SetupScriptsExecutionSettings(channel);
            if (!File.Exists(executionSettings.ScriptsFullPath))
            {
                // The script root is repo-local and may not exist yet, so make sure it is there before downloading into it.
                Directory.CreateDirectory(DotNetInstallScriptRootPath);
                AsyncTasks.Task.Run(() => DownloadScriptAsync(executionSettings.ScriptName, executionSettings.ScriptsFullPath)).GetAwaiter().GetResult();
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            MakeScriptExecutable(executionSettings.ScriptsFullPath);

            if (!RunScript(executionSettings))
            {
                return false;
            }

            string? installedVersion = UseDailySdk ? _reportedSdkVersion : Version;
            if (installedVersion is null || !s_sdkVersionPattern.IsMatch(installedVersion))
            {
                Log.LogError("The dotnet-install script did not identify a valid installed SDK version. Rebuild the bootstrap with a current install script or specify BootstrapSdkChannel explicitly.");
                return false;
            }

            string sdkDirectory = Path.Combine(InstallDir, "sdk", installedVersion);
            if (!Directory.Exists(sdkDirectory))
            {
                Log.LogError($"The dotnet-install script succeeded, but SDK '{installedVersion}' was not found in '{sdkDirectory}'. Rebuild the bootstrap to repair the incomplete installation.");
                return false;
            }

            InstalledSdkVersion = installedVersion;
            return true;
        }

        protected override string GenerateFullPathToTool() => ToolName;

        // Do not use the normal parse-for-canonical-errors mechanism since install-scripts can emit nonfatal curl errors that match that pattern.
        // Instead, log everything as a message and rely on the final success/failure return.
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            Log.LogMessage(messageImportance, singleLine);

            if (UseDailySdk)
            {
                Match match = s_installedVersionPattern.Match(singleLine);
                if (match.Success)
                {
                    string version = match.Groups["version"].Value;
                    if (_reportedSdkVersion is not null && !string.Equals(_reportedSdkVersion, version, StringComparison.Ordinal))
                    {
                        Log.LogError("The dotnet-install script reported multiple installed SDK versions. Rebuild the bootstrap with a current install script.");
                    }

                    _reportedSdkVersion = version;
                }
            }
        }

        private async AsyncTasks.Task<string?> DiscoverMainSdkChannelAsync()
        {
            string? buildsTable = await DownloadContentAsync(
                MainBuildsTableUrl,
                "Daily bootstrap SDK channel discovery",
                " Set BootstrapSdkChannel explicitly to skip automatic main-channel discovery.").ConfigureAwait(false);
            if (buildsTable is null)
            {
                return null;
            }

            string? channel = null;
            for (Match match = s_mainSdkLinkPattern.Match(buildsTable); match.Success; match = match.NextMatch())
            {
                string linkChannel = match.Groups["channel"].Value;
                if (channel is not null && !string.Equals(channel, linkChannel, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogError($"The published dotnet/dotnet main SDK links specify different channels, '{channel}' and '{linkChannel}'. Set BootstrapSdkChannel explicitly to select a daily channel.");
                    return null;
                }

                channel = linkChannel;
            }

            if (channel is null)
            {
                Log.LogError($"No main daily SDK channel was found in '{MainBuildsTableUrl}'. Set BootstrapSdkChannel explicitly to select a daily channel.");
            }

            return channel;
        }

        /// <summary>
        /// Downloads the .NET Core installation script asynchronously from the specified URL.
        /// </summary>
        /// <param name="scriptName">The name of the script to download.</param>
        /// <param name="scriptPath">The path where the script will be saved.</param>
        private async AsyncTasks.Task DownloadScriptAsync(string scriptName, string scriptPath)
        {
            string? scriptContent = await DownloadScriptContentAsync(scriptName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(scriptContent))
            {
                File.WriteAllText(scriptPath, scriptContent);
            }
        }

        private AsyncTasks.Task<string?> DownloadScriptContentAsync(string scriptName)
            => DownloadContentAsync($"{DotNetInstallBaseUrl}{scriptName}", "Install-scripts");

        private async AsyncTasks.Task<string?> DownloadContentAsync(string url, string description, string failureAction = "")
        {
#pragma warning disable CA2000 // The default handler is disposed by HttpClient; an injected handler belongs to its caller.
            using (HttpClient client = new HttpClient(HttpMessageHandler ?? new HttpClientHandler(), disposeHandler: HttpMessageHandler is null))
#pragma warning restore CA2000
            {
                using (HttpResponseMessage? response = await GetDownloadResponseAsync(client, url, description, failureAction).ConfigureAwait(false))
                {
                    if (response is null)
                    {
                        return null;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    Log.LogError($"{description} download from {url} error. Status code: {response.StatusCode}.{failureAction}");
                    return null;
                }
            }
        }

        private async AsyncTasks.Task<HttpResponseMessage?> GetDownloadResponseAsync(HttpClient client, string url, string description, string failureAction)
        {
            for (int attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
            {
                try
                {
                    return await client.GetAsync(url).ConfigureAwait(false);
                }
                catch (HttpRequestException e)
                {
                    if (attempt < MaxDownloadAttempts)
                    {
                        Log.LogMessage(MessageImportance.Low, $"{description} download from {url} failed. Retrying attempt {attempt + 1} of {MaxDownloadAttempts}. {e.Message}");
                        continue;
                    }

                    Log.LogError($"{description} download from {url} failed after {attempt} {(attempt == 1 ? "attempt" : "attempts")}. {e.Message}{failureAction}");
                    Log.LogMessage(MessageImportance.Low, e.ToString());
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Makes the installation script executable on non-Windows platforms.
        /// </summary>
        /// <param name="scriptPath">The path of the script to make executable.</param>
        private void MakeScriptExecutable(string scriptPath)
        {
            if (!IsWindows)
            {
                int exitCode = ExecuteTool("/bin/chmod", string.Empty, $"+x \"{scriptPath}\"");
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
        private ScriptExecutionSettings SetupScriptsExecutionSettings(string? channel)
        {
            string scriptExtension = IsWindows ? "ps1" : "sh";
            string scriptPath = Path.Combine(DotNetInstallScriptRootPath, $"{ScriptName}.{scriptExtension}");
            // On Windows the native command-line parser treats a backslash before the closing quote as
            // escaping it (e.g. "C:\dir\" becomes C:\dir"), so trim a trailing separator before quoting.
            string installDir = TrimTrailingDirectorySeparators(InstallDir);
            string scriptArgs = GenerateScriptArguments(IsWindows, scriptPath, installDir, Version, channel);

            return new ScriptExecutionSettings($"{ScriptName}.{scriptExtension}", scriptPath, scriptArgs);
        }

        private static string GenerateScriptArguments(bool isWindows, string scriptPath, string installDir, string version, string? channel)
        {
            string sdkArgs = channel is not null
                ? (isWindows ? $"-Channel \"{channel}\" -Quality daily" : $"--channel \"{channel}\" --quality daily")
                : (isWindows ? $"-Version {version}" : $"--version {version}");
            return isWindows
                ? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {sdkArgs} -InstallDir \"{installDir}\""
                : $"\"{scriptPath}\" {sdkArgs} --install-dir \"{installDir}\"";
        }

        /// <summary>
        /// Trims trailing directory separators while preserving a path root (e.g. "C:\").
        /// </summary>
        private static string TrimTrailingDirectorySeparators(string path)
        {
            string root = Path.GetPathRoot(path) ?? string.Empty;
            while (path.Length > root.Length &&
                   (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                    path[path.Length - 1] == Path.AltDirectorySeparatorChar))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
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
