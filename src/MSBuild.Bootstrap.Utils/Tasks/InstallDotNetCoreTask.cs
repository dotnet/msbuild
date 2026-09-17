// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
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
        private const int MaxScriptDownloadAttempts = 3;

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
        /// Gets or sets the base URL for downloading the .NET Core installation script. The default value is "https://dot.net/v1/".
        /// </summary>
        public string DotNetInstallBaseUrl { get; set; } = "https://dot.net/v1/";

        internal HttpMessageHandler HttpMessageHandler { get; set; } = null!;

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
                // The script root is repo-local and may not exist yet, so make sure it is there before downloading into it.
                Directory.CreateDirectory(DotNetInstallScriptRootPath);
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
            string scriptContent = await DownloadScriptContentAsync(scriptName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(scriptContent))
            {
                File.WriteAllText(scriptPath, scriptContent);
            }
        }

        private async AsyncTasks.Task<string> DownloadScriptContentAsync(string scriptName)
        {
            string scriptUrl = $"{DotNetInstallBaseUrl}{scriptName}";

#pragma warning disable CA2000 // Dispose objects before losing scope because HttpClientHandler is disposed by HttpClient.Dispose()
            using (HttpClient client = new HttpClient(HttpMessageHandler ?? new HttpClientHandler(), disposeHandler: true))
#pragma warning restore CA2000
            {
                for (int attempt = 1; attempt <= MaxScriptDownloadAttempts; attempt++)
                {
                    try
                    {
                        using (HttpResponseMessage response = await client.GetAsync(scriptUrl).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            }

                            Log.LogError($"Install-scripts download from {DotNetInstallBaseUrl} error. Status code: {response.StatusCode}.");
                            return null!;
                        }
                    }
                    catch (Exception e) when (IsScriptDownloadTransportFailure(e))
                    {
                        string flattenedMessage = GetInnerExceptionMessageString(e);

                        if (attempt < MaxScriptDownloadAttempts && IsTransientTransportFailure(e))
                        {
                            Log.LogMessage(MessageImportance.Low, $"Install-scripts download from {scriptUrl} failed with a transient transport error. Retrying attempt {attempt + 1} of {MaxScriptDownloadAttempts}. {flattenedMessage}");
                            continue;
                        }

                        Log.LogError($"Install-scripts download from {scriptUrl} failed after {attempt} {(attempt == 1 ? "attempt" : "attempts")}. {flattenedMessage}");
                        Log.LogMessage(MessageImportance.Low, e.ToString());
                        return null!;
                    }
                }
            }

            return null!;
        }

        private static bool IsScriptDownloadTransportFailure(Exception exception)
        {
            return exception is HttpRequestException or IOException or SocketException;
        }

        private static bool IsTransientTransportFailure(Exception exception)
        {
            if (exception is HttpRequestException httpRequestException)
            {
                return httpRequestException.InnerException switch
                {
                    AuthenticationException => false,
                    IOException => true,
                    SocketException socketException => IsTransientSocketException(socketException),
                    _ => false
                };
            }

            return exception is IOException ||
                (exception is SocketException directSocketException && IsTransientSocketException(directSocketException));
        }

        private static bool IsTransientSocketException(SocketException socketException)
        {
            return socketException.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.NetworkReset or SocketError.TimedOut;
        }

        private static string GetInnerExceptionMessageString(Exception exception)
        {
            StringBuilder message = new StringBuilder(exception.Message);

            for (Exception current = exception.InnerException; current is not null; current = current.InnerException)
            {
                message.Append(' ');
                message.Append(current.Message);
            }

            return message.ToString();
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
        private ScriptExecutionSettings SetupScriptsExecutionSettings()
        {
            string scriptExtension = IsWindows ? "ps1" : "sh";
            string scriptPath = Path.Combine(DotNetInstallScriptRootPath, $"{ScriptName}.{scriptExtension}");
            // On Windows the native command-line parser treats a backslash before the closing quote as
            // escaping it (e.g. "C:\dir\" becomes C:\dir"), so trim a trailing separator before quoting.
            string installDir = TrimTrailingDirectorySeparators(InstallDir);
            string scriptArgs = IsWindows
                ? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Version {Version} -InstallDir \"{installDir}\""
                : $"\"{scriptPath}\" --version {Version} --install-dir \"{installDir}\"";

            return new ScriptExecutionSettings($"{ScriptName}.{scriptExtension}", scriptPath, scriptArgs);
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
