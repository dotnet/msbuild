// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class RunReadyToRunCompiler : ToolTask
    {
        public ITaskItem CrossgenTool { get; set; }
        public ITaskItem Crossgen2Tool { get; set; }

        [Required]
        public ITaskItem CompilationEntry { get; set; }
        [Required]
        public ITaskItem[] ImplementationAssemblyReferences { get; set; }
        public bool ShowCompilerWarnings { get; set; }
        public bool UseCrossgen2 { get; set; }
        public string Crossgen2ExtraCommandLineArgs { get; set; }

        [Output]
        public bool WarningsDetected { get; set; }

        private string _inputAssembly;
        private string _outputR2RImage;
        private string _outputPDBImage;
        private string _createPDBCommand;

        private bool IsPdbCompilation => !String.IsNullOrEmpty(_createPDBCommand);

        protected override string ToolName
        {
            get
            {
                // NOTE: Crossgen2 does not yet support emitting native symbols. We use crossgen instead for now.
                if (UseCrossgen2)
                {
                    return IsPdbCompilation ? CrossgenTool.ItemSpec : Crossgen2Tool.ItemSpec;
                }
                else
                {
                    return CrossgenTool.ItemSpec;
                }
            }
        }

        protected override string GenerateFullPathToTool() => ToolName;

        // NOTE: Crossgen2 does not yet support emitting native symbols. We use crossgen instead for now.
        private string DiaSymReader => CrossgenTool.GetMetadata("DiaSymReader");

        public RunReadyToRunCompiler()
        {
            LogStandardErrorAsError = true;
        }

        protected override bool ValidateParameters()
        {
            _createPDBCommand = CompilationEntry.GetMetadata("CreatePDBCommand");

            if (CrossgenTool == null && Crossgen2Tool == null)
            {
                return false;
            }
            if (IsPdbCompilation && CrossgenTool == null)
            {
                // We need the crossgen tool for now to emit native symbols. Crossgen2 does not yet support this feature
                return false;
            }

            if(CrossgenTool != null)
            {
                if (!File.Exists(CrossgenTool.ItemSpec) || !File.Exists(CrossgenTool.GetMetadata("JitPath")))
                {
                    return false;
                }
            }
            if(Crossgen2Tool != null)
            {
                if (!File.Exists(Crossgen2Tool.ItemSpec) || !File.Exists(Crossgen2Tool.GetMetadata("JitPath")))
                {
                    return false;
                }
            }

            if (IsPdbCompilation)
            {
                _outputR2RImage = CompilationEntry.ItemSpec;
                _outputPDBImage = CompilationEntry.GetMetadata("OutputPDBImage");

                if (!String.IsNullOrEmpty(DiaSymReader) && !File.Exists(DiaSymReader))
                {
                    return false;
                }

                // R2R image has to be created before emitting native symbols (crossgen needs this as an input argument)
                if (String.IsNullOrEmpty(_outputPDBImage) || !File.Exists(_outputR2RImage))
                {
                    return false;
                }
            }
            else
            {
                _inputAssembly = CompilationEntry.ItemSpec;
                _outputR2RImage = CompilationEntry.GetMetadata("OutputR2RImage");

                if (!File.Exists(_inputAssembly))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetAssemblyReferencesCommands()
        {
            StringBuilder result = new StringBuilder();

            foreach (var reference in ImplementationAssemblyReferences)
            {
                // When generating PDBs, we must not add a reference to the IL version of the R2R image for which we're trying to generate a PDB
                if (IsPdbCompilation && String.Equals(Path.GetFileName(reference.ItemSpec), Path.GetFileName(_outputR2RImage), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (UseCrossgen2 && !IsPdbCompilation)
                {
                    result.AppendLine($"-r:\"{reference}\"");
                }
                else
                {
                    result.AppendLine($"-r \"{reference}\"");
                }
            }

            return result.ToString();
        }

        protected override string GenerateResponseFileCommands()
        {
            // NOTE: Crossgen2 does not yet support emitting native symbols. We use crossgen instead for now.
            if (IsPdbCompilation)
            {
                return GenerateCrossgenResponseFile();
            }
            else
            {
                return UseCrossgen2 ? GenerateCrossgen2ResponseFile() : GenerateCrossgenResponseFile();
            }
        }

        private string GenerateCrossgenResponseFile()
        {
            StringBuilder result = new StringBuilder();

            result.AppendLine("/nologo");

            if (IsPdbCompilation)
            {
                result.Append(GetAssemblyReferencesCommands());

                if (!String.IsNullOrEmpty(DiaSymReader))
                {
                    result.AppendLine($"/DiasymreaderPath \"{DiaSymReader}\"");
                }

                result.AppendLine(_createPDBCommand);
                result.AppendLine($"\"{_outputR2RImage}\"");
            }
            else
            {
                result.AppendLine("/MissingDependenciesOK");
                result.AppendLine($"/JITPath \"{CrossgenTool.GetMetadata("JitPath")}\"");
                result.Append(GetAssemblyReferencesCommands());
                result.AppendLine($"/out \"{_outputR2RImage}\"");
                result.AppendLine($"\"{_inputAssembly}\"");
            }

            return result.ToString();
        }

        private string GenerateCrossgen2ResponseFile()
        {
            StringBuilder result = new StringBuilder();

            result.AppendLine("-O");
            result.AppendLine($"--jitpath:\"{Crossgen2Tool.GetMetadata("JitPath")}\"");
            result.Append(GetAssemblyReferencesCommands());
            result.AppendLine($"--out:\"{_outputR2RImage}\"");
            if (!String.IsNullOrEmpty(Crossgen2ExtraCommandLineArgs))
            {
                result.AppendLine(Crossgen2ExtraCommandLineArgs);
            }
            // Note: do not add double quotes around the input assembly, even if the file path contains spaces. The command line 
            // parsing logic will append this string to the working directory if it's a relative path, so any double quotes will result in errors.
            result.AppendLine($"{_inputAssembly}");

            return result.ToString();
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            // Ensure output sub-directories exists - Crossgen does not create directories for output files. Any relative path used with the 
            // '/out' parameter has to have an existing directory.
            Directory.CreateDirectory(Path.GetDirectoryName(_outputR2RImage));

            WarningsDetected = false;

            return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            if (!ShowCompilerWarnings && singleLine.IndexOf("warning:", StringComparison.OrdinalIgnoreCase) != -1)
            {
                Log.LogMessage(MessageImportance.Normal, singleLine);
                WarningsDetected = true;
            }
            else
            {
                base.LogEventsFromTextOutput(singleLine, messageImportance);
            }
        }
    }
}
