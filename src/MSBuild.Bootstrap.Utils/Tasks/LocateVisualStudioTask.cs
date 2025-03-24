// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Bootstrap.Utils.Tasks
{
    public class LocateVisualStudioTask : ToolTask
    {
        private StringBuilder _standardOutput = new StringBuilder();

        [Output]
        public string VsInstallPath { get; set; }

        protected override string ToolName => "powershell.exe";

        protected override string GenerateFullPathToTool() => ToolName;

        protected override string GenerateCommandLineCommands()
        {
            string script = @"
                $vsWherePath = ""${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe""
                if (Test-Path $vsWherePath) {
                    try {
                        $vsPath = & $vsWherePath -latest -property installationPath
                        if ($vsPath -and (Test-Path $vsPath)) {
                            Write-Output $vsPath
                            exit 0
                        }
                    } catch {
                        Write-Warning ""VSWhere failed: $_""
                    }
                }

                # No installation found
                exit 1
            ";

            script = script.Replace("\"", "\\\"");

            return $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"";
        }

        public override bool Execute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.LogMessage(MessageImportance.High, "Not running on Windows. Skipping Visual Studio detection.");
                return true;
            }

            _ = ExecuteTool(ToolName, string.Empty, GenerateCommandLineCommands());

            if (!Log.HasLoggedErrors)
            {
                VsInstallPath = _standardOutput.ToString().Trim();
            }

            return true;
        }

        // Override to capture standard output
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            if (!string.IsNullOrWhiteSpace(singleLine))
            {
                _ = _standardOutput.AppendLine(singleLine);
            }

            base.LogEventsFromTextOutput(singleLine, messageImportance);
        }
    }
}
