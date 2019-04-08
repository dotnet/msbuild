// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class RunReadyToRunCompiler : ToolTask
    {
        [Required]
        public ITaskItem CrossgenTool { get; set; }
        [Required]
        public ITaskItem CompilationEntry { get; set; }

        private string _crossgenPath;
        private string _clrjitPath;
        private string _diasymreaderPath;

        private string _inputAssembly;
        private string _outputR2RImage;
        private string _outputPDBImage;
        private string _platformAssembliesPaths;
        private string _createPDBCommand;

        private bool IsPdbCompilation => !String.IsNullOrEmpty(_createPDBCommand);

        protected override string ToolName => _crossgenPath;

        protected override string GenerateFullPathToTool() => _crossgenPath;

        public RunReadyToRunCompiler()
        {
            LogStandardErrorAsError = true;
        }

        protected override bool ValidateParameters()
        {
            _crossgenPath = CrossgenTool.ItemSpec;
            _clrjitPath = CrossgenTool.GetMetadata("JitPath");
            _diasymreaderPath = CrossgenTool.GetMetadata("DiaSymReader");

            if (!File.Exists(_crossgenPath) || !File.Exists(_clrjitPath))
            {
                return false;
            }

            _createPDBCommand = CompilationEntry.GetMetadata("CreatePDBCommand");
            _platformAssembliesPaths = CompilationEntry.GetMetadata("PlatformAssembliesPaths");

            if (IsPdbCompilation)
            {
                _outputR2RImage = CompilationEntry.ItemSpec;
                _outputPDBImage = CompilationEntry.GetMetadata("OutputPDBImage");

                if (!String.IsNullOrEmpty(_diasymreaderPath) && !File.Exists(_diasymreaderPath))
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

        protected override string GenerateCommandLineCommands()
        {
            if (IsPdbCompilation)
            {
                return String.IsNullOrEmpty(_diasymreaderPath) ?
                    $"/nologo /Platform_Assemblies_Paths \"{_platformAssembliesPaths}\" {_createPDBCommand} \"{_outputR2RImage}\"" :
                    $"/nologo /Platform_Assemblies_Paths \"{_platformAssembliesPaths}\" /DiasymreaderPath \"{_diasymreaderPath}\" {_createPDBCommand} \"{_outputR2RImage}\"";
            }
            else
            {
                return $"/nologo /MissingDependenciesOK /JITPath \"{_clrjitPath}\" /Platform_Assemblies_Paths \"{_platformAssembliesPaths}\" /out \"{_outputR2RImage}\" \"{_inputAssembly}\"";
            }
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            // Ensure output sub-directories exists - Crossgen does not create directories for output files. Any relative path used with the 
            // '/out' parameter has to have an existing directory.
            Directory.CreateDirectory(Path.GetDirectoryName(_outputR2RImage));

            return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
        }
    }
}
