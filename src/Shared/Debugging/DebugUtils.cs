// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Shared.Debugging
{
    internal static class DebugUtils
    {
        static DebugUtils()
        {
            SetDebugPath();
        }

        // DebugUtils are initialized early on by the test runner - during preparing data for DataMemeberAttribute of some test,
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
                    debugDirectory = Path.Combine(debugDirectory, ".MSBuild_Logs");
                }
                else if (FileUtilities.CanWriteToDirectory(Directory.GetCurrentDirectory()))
                {
                    debugDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".MSBuild_Logs");
                }
                else
                {
                    debugDirectory = Path.Combine(FileUtilities.TempFileDirectory, ".MSBuild_Logs");
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
        () =>
        {
            return ScanNodeMode(Environment.CommandLine);

            NodeMode? ScanNodeMode(string input)
            {
                var match = Regex.Match(input, @"/nodemode:(?<nodemode>[1-9]\d*)(\s|$)", RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    return null; // Central/main process (not running as a node)
                }
                var nodeMode = match.Groups["nodemode"].Value;

                Trace.Assert(!string.IsNullOrEmpty(nodeMode));

                // Try to parse using the shared NodeModeHelper
                if (NodeModeHelper.TryParse(nodeMode, out NodeMode? parsedMode))
                {
                    return parsedMode;
                }

                // If parsing fails, this is an unknown/unsupported node mode
                return null;
            }
        });

        private static bool CurrentProcessMatchesDebugName()
        {
            var processNameToBreakInto = Environment.GetEnvironmentVariable("MSBuildDebugProcessName");
            var thisProcessMatchesName = string.IsNullOrWhiteSpace(processNameToBreakInto) ||
                                         EnvironmentUtilities.ProcessName.Contains(processNameToBreakInto);

            return thisProcessMatchesName;
        }

        public static readonly string ProcessInfoString =
            $"{(ProcessNodeMode.Value?.ToString() ?? "CentralNode")}_{EnvironmentUtilities.ProcessName}_PID={EnvironmentUtilities.CurrentProcessId}_x{(Environment.Is64BitProcess ? "64" : "86")}";

        public static readonly bool ShouldDebugCurrentProcess = CurrentProcessMatchesDebugName();

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

        public static string FindNextAvailableDebugFilePath(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var fullPath = Path.Combine(DebugPath, fileName);

            var counter = 0;
            while (FileSystems.Default.FileExists(fullPath))
            {
                fileName = $"{fileNameWithoutExtension}_{counter++}{extension}";
                fullPath = Path.Combine(DebugPath, fileName);
            }

            return fullPath;
        }
    }
}
