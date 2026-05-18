// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Bootstrap.Utils.Tasks
{
    public class LocateVisualStudioTask : ToolTask
    {
        private readonly StringBuilder _standardOutput = new();

        [Output]
        public string VsInstallPath { get; set; }

        protected override string ToolName => "vswhere.exe";

        protected override string GenerateFullPathToTool()
        {
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string vsWherePath = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", ToolName);


            return vsWherePath;
        }

        protected override string GenerateCommandLineCommands() => "-latest -prerelease -property installationPath";

        public override bool Execute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.LogMessage(MessageImportance.High, "Not running on Windows. Skipping Visual Studio detection.");
                return true;
            }

            _ = ExecuteTool(GenerateFullPathToTool(), string.Empty, GenerateCommandLineCommands());

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
