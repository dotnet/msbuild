// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class Crossgen : ToolTask
    {
        [Required]
        public string SourceAssembly { get;set; }

        [Required]
        public string DestinationPath { get; set; }

        [Required]
        public string JITPath { get; set; }

        public string CrossgenPath { get; set; }

        public bool CreateSymbols { get; set; }

        public string DiasymReaderPath { get; set; }

        public bool ReadyToRun { get; set; }

        public ITaskItem[] PlatformAssemblyPaths { get; set; }

        private string TempOutputPath { get; set; }

        private bool _secondInvocationToCreateSymbols;

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            if (!File.Exists(SourceAssembly))
            {
                Log.LogError($"SourceAssembly '{SourceAssembly}' does not exist.");

                return false;
            }

            return true;
        }

        public override bool Execute()
        {
            TempOutputPath = Path.GetTempFileName();

            var toolResult = base.Execute();

            if (toolResult)
            {
                File.Copy(TempOutputPath, DestinationPath, overwrite: true);
            }

            if (File.Exists(TempOutputPath))
            {
                File.Delete(TempOutputPath);
            }

            if (toolResult && CreateSymbols)
            {
                _secondInvocationToCreateSymbols = true;
                toolResult = base.Execute();
            }

            return toolResult;
        }

        protected override string ToolName
        {
            get { return "crossgen"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            if (CrossgenPath != null)
            {
                return CrossgenPath;
            }

            return "crossgen";
        }

        protected override string GenerateCommandLineCommands()
        {
            if (_secondInvocationToCreateSymbols)
            {
                return $"{GetReadyToRun()} {GetPlatformAssemblyPaths()} {GetDiasymReaderPath()} {GetCreateSymbols()}";
            }

            return $"{GetReadyToRun()} {GetInPath()} {GetOutPath()} {GetPlatformAssemblyPaths()} {GetJitPath()}";
        }

        private string GetCreateSymbols()
        {
            var option = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-createpdb" : "-createperfmap";
            return $"{option} \"{Path.GetDirectoryName(DestinationPath)}\" \"{DestinationPath}\"";
        }

        private string GetDiasymReaderPath()
        {
            if (string.IsNullOrEmpty(DiasymReaderPath))
            {
                return null;
            }

            return $"-diasymreaderpath \"{DiasymReaderPath}\"";
        }

        private string GetReadyToRun()
        {
            if (ReadyToRun)
            {
                return "-readytorun";
            }

            return null;
        }

        private string GetInPath()
        {
            return $"-in \"{SourceAssembly}\"";
        }
        
        private string GetOutPath()
        {
            return $"-out \"{TempOutputPath}\"";
        }

        private string GetPlatformAssemblyPaths()
        {
            var platformAssemblyPaths = String.Empty;

            if (PlatformAssemblyPaths != null)
            {
                foreach (var excludeTaskItem in PlatformAssemblyPaths)
                {
                    platformAssemblyPaths += $"{excludeTaskItem.ItemSpec}{Path.PathSeparator}";
                }
            }
            
            return $" -platform_assemblies_paths {platformAssemblyPaths.Trim(':')}";
        }
        
        private string GetJitPath()
        {
            return $"-JITPath {JITPath}";
        }

        protected override void LogToolCommand(string message)
        {
            base.LogToolCommand($"{base.GetWorkingDirectory()}> {message}");
        }
    }
}
