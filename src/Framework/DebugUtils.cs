// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

#nullable disable

namespace Microsoft.Build.Framework
{
    internal static class FrameworkDebugUtils
    {
#pragma warning disable CA1810 // Intentional: static constructor catches exceptions to prevent TypeInitializationException
        static FrameworkDebugUtils()
#pragma warning restore CA1810
        {
            try
            {
                SetDebugPath();
            }
            catch (Exception ex)
            {
                // A failure in SetDebugPath must not prevent MSBuild from starting.
                // DebugPath will remain null — debugging/logging features will be
                // unavailable for this session, but the build can still proceed.
                //
                // Known failure scenarios:
                // - Directory.GetCurrentDirectory() throws DirectoryNotFoundException
                //   if the working directory was deleted before MSBuild started.
                // - FileUtilities.EnsureDirectoryExists() throws UnauthorizedAccessException
                //   or IOException when the target path is on a read-only volume or an
                //   offline network share.
                // - Path.Combine() throws ArgumentException when MSBUILDDEBUGPATH contains
                //   illegal path characters (e.g., '<', '>', '|').
                // - PathTooLongException when the resolved path exceeds MAX_PATH on
                //   .NET Framework without long-path support.
                try
                {
                    Console.Error.WriteLine("MSBuild debug path initialization failed: " + ex);
                }
                catch
                {
                    // Console may not be available.
                }
            }

            // Initialize diagnostic fields inside the static constructor so failures
            // are caught here rather than poisoning the type with an unrecoverable
            // TypeInitializationException. On .NET Framework, EnvironmentUtilities
            // accesses Process.GetCurrentProcess() which can throw Win32Exception
            // in restricted environments or when performance counters are corrupted.
            try
            {
                ProcessInfoString = GetProcessInfoString();
                ShouldDebugCurrentProcess = CurrentProcessMatchesDebugName();
            }
            catch
            {
                ProcessInfoString ??= "Unknown";
                ShouldDebugCurrentProcess = false;
            }
        }

        // FrameworkDebugUtils is initialized early on by the test runner - during preparing data for DataMemberAttribute of some test,
        // for that reason it is not easily possible to inject the DebugPath in tests via env var (unless we want to run expensive exec style test).
        internal static void SetDebugPath()
        {
            string environmentDebugPath = FileUtilities.TrimAndStripAnyQuotes(Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH"));
            string debugDirectory = environmentDebugPath;
            if (Traits.Instance.DebugEngine)
            {
                if (!string.IsNullOrWhiteSpace(debugDirectory) && FileUtilities.CanWriteToDirectory(debugDirectory))
                {
                    // Add a dedicated ".MSBuild_Logs" folder inside the user-specified path, either always or when in solution directory.
                    debugDirectory = System.IO.Path.Combine(debugDirectory, ".MSBuild_Logs");
                }
                else if (FileUtilities.CanWriteToDirectory(Directory.GetCurrentDirectory()))
                {
                    debugDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".MSBuild_Logs");
                }
                else
                {
                    debugDirectory = System.IO.Path.Combine(FileUtilities.TempFileDirectory, ".MSBuild_Logs");
                }

                // Out of proc nodes do not know the startup directory so set the environment variable for them.
                if (string.IsNullOrWhiteSpace(environmentDebugPath))
                {
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", debugDirectory);
                }
            }

            if (debugDirectory is not null)
            {
                FileUtilities.EnsureDirectoryExists(debugDirectory);
            }

            DebugPath = debugDirectory;
        }

        private static readonly Lazy<NodeMode?> ProcessNodeMode = new(
            () => NodeModeHelper.ExtractFromCommandLine(Environment.CommandLine));

        private static bool CurrentProcessMatchesDebugName()
        {
            var processNameToBreakInto = Environment.GetEnvironmentVariable("MSBuildDebugProcessName");
            var thisProcessMatchesName = string.IsNullOrWhiteSpace(processNameToBreakInto) ||
                                         EnvironmentUtilities.ProcessName.Contains(processNameToBreakInto);

            return thisProcessMatchesName;
        }

        /// <summary>
        /// Builds a diagnostic string identifying this process (node mode, name, PID, bitness).
        /// Must be called from the static constructor rather than as a field initializer because
        /// on .NET Framework, <see cref="EnvironmentUtilities.ProcessName"/> and
        /// <see cref="EnvironmentUtilities.CurrentProcessId"/> access
        /// <c>Process.GetCurrentProcess()</c> which can throw <see cref="System.ComponentModel.Win32Exception"/>
        /// in restricted environments or when performance counters are corrupted.
        /// A field-initializer failure would produce an unrecoverable <see cref="TypeInitializationException"/>
        /// that poisons the entire <see cref="FrameworkDebugUtils"/> type, whereas the static constructor's
        /// try/catch lets the type initialize successfully with a safe fallback value.
        /// </summary>
        private static string GetProcessInfoString() => $"{(ProcessNodeMode.Value?.ToString() ?? "CentralNode")}_{EnvironmentUtilities.ProcessName}_PID={EnvironmentUtilities.CurrentProcessId}_x{(Environment.Is64BitProcess ? "64" : "86")}";

        public static readonly string ProcessInfoString;

        public static readonly bool ShouldDebugCurrentProcess;

        public static string DebugPath { get; private set; }
        
        /// <summary>
        /// Returns true if the current process is an out-of-proc TaskHost node.
        /// </summary>
        /// <returns>
        /// True if this process was launched with /nodemode:2 (indicating it's a TaskHost process),
        /// false otherwise. This is useful for conditionally enabling debugging or other behaviors
        /// based on whether the code is running in the main MSBuild process or a child TaskHost process.
        /// </returns>
        public static bool IsInTaskHostNode() => ProcessNodeMode.Value == NodeMode.OutOfProcTaskHostNode;
    }
}
