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
        private enum NodeMode
        {
            CentralNode,
            OutOfProcNode,
            OutOfProcTaskHostNode
        }

        static DebugUtils()
        {
            SetDebugPath();
        }

        // DebugUtils are initialized early on by the test runner - during preparing data for DataMemeberAttribute of some test,
        //  for that reason it is not easily possible to inject the DebugPath in tests via env var (unless we want to run expensive exec style test).
        internal static void SetDebugPath()
        {
            string environmentDebugPath = FileUtilities.TrimAndStripAnyQuotes(Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH"));
            string debugDirectory = environmentDebugPath;

            if (Traits.Instance.DebugEngine)
            {
                if (!string.IsNullOrWhiteSpace(debugDirectory) && FileUtilities.CanWriteToDirectory(debugDirectory) && !IsPathInSolutionDirectory(debugDirectory))
                {
                    // Debug directory is writable; no need for fallbacks
                }
                else if (!string.IsNullOrWhiteSpace(debugDirectory) && IsPathInSolutionDirectory(debugDirectory))
                {
                    // Redirect to temp to avoid infinite build loops in Visual Studio
                    debugDirectory = Path.Combine(FileUtilities.TempFileDirectory, "MSBuild_Logs");
                }
                else if (FileUtilities.CanWriteToDirectory(Directory.GetCurrentDirectory()))
                {
                    debugDirectory = Path.Combine(Directory.GetCurrentDirectory(), "MSBuild_Logs");
                }
                else
                {
                    debugDirectory = Path.Combine(FileUtilities.TempFileDirectory, "MSBuild_Logs");
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

        private static readonly Lazy<NodeMode> ProcessNodeMode = new(
        () =>
        {
            return ScanNodeMode(Environment.CommandLine);

            NodeMode ScanNodeMode(string input)
            {
                var match = Regex.Match(input, @"/nodemode:(?<nodemode>[12\s])(\s|$)", RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    return NodeMode.CentralNode;
                }
                var nodeMode = match.Groups["nodemode"].Value;

                Trace.Assert(!string.IsNullOrEmpty(nodeMode));

                return nodeMode switch
                {
                    "1" => NodeMode.OutOfProcNode,
                    "2" => NodeMode.OutOfProcTaskHostNode,
                    _ => throw new NotImplementedException(),
                };
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
            $"{ProcessNodeMode.Value}_{EnvironmentUtilities.ProcessName}_PID={EnvironmentUtilities.CurrentProcessId}_x{(Environment.Is64BitProcess ? "64" : "86")}";

        public static readonly bool ShouldDebugCurrentProcess = CurrentProcessMatchesDebugName();

        public static string DebugPath { get; private set; }

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

        private static bool IsPathInSolutionDirectory(string debugPath)
        {
            if (string.IsNullOrWhiteSpace(debugPath))
            {
                return false;
            }
            try
            {
                string resolvedPath = Path.GetFullPath(debugPath).TrimEnd(Path.DirectorySeparatorChar);
                string currentDir = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar);

                // On macOS, when current directory is in temp folder, Path.GetFullPath() 
                // return paths with /private prefix while environment variables don't.
                // Normalize both paths to ensure consistent comparison.
                if (NativeMethodsShared.IsOSX)
                {
                    resolvedPath = NormalizePath(resolvedPath);
                    currentDir = NormalizePath(currentDir);
                }
                return resolvedPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // On macOS, remove /private prefix if present to ensure consistent path comparison
            // This is needed when current directory is in temp folder, as Path.GetFullPath() 
            // may return paths with /private prefix while environment variables don't
            if (path.StartsWith("/private/", StringComparison.Ordinal))
            {
                return path.Substring(8); // Remove "/private" (8 characters)
            }

            return path;
        }
    }
}
