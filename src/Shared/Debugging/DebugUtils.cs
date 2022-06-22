// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;

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
            string environmentDebugPath = FileUtilities.TrimAndStripAnyQuotes(Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH"));
            string debugDirectory = environmentDebugPath;

            if (Traits.Instance.DebugEngine)
            {
                if (!string.IsNullOrWhiteSpace(debugDirectory) && FileUtilities.CanWriteToDirectory(debugDirectory))
                {
                    // Debug directory is writable; no need for fallbacks
                }
                else if (FileUtilities.CanWriteToDirectory(Directory.GetCurrentDirectory()))
                {
                    debugDirectory = Path.Combine(Directory.GetCurrentDirectory(), "MSBuild_Logs");
                }
                else
                {
                    debugDirectory = Path.Combine(Path.GetTempPath(), "MSBuild_Logs");
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
                                         Process.GetCurrentProcess().ProcessName.Contains(processNameToBreakInto);

            return thisProcessMatchesName;
        }

        public static readonly string ProcessInfoString =
            $"{ProcessNodeMode.Value}_{Process.GetCurrentProcess().ProcessName}_PID={Process.GetCurrentProcess().Id}_x{(Environment.Is64BitProcess ? "64" : "86")}";

        public static readonly bool ShouldDebugCurrentProcess = CurrentProcessMatchesDebugName();

        public static string DebugPath { get; }

        public static string FindNextAvailableDebugFilePath(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var fullPath = Path.Combine(DebugPath, fileName);

            var counter = 0;
            while (File.Exists(fullPath))
            {
                fileName = $"{fileNameWithoutExtension}_{counter++}{extension}";
                fullPath = Path.Combine(DebugPath, fileName);
            }

            return fullPath;
        }
    }
}
