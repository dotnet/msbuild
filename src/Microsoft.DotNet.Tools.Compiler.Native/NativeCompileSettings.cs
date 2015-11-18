using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class NativeCompileSettings
    {
        public string LogPath { get; set; }
        public string InputManagedAssemblyPath { get; set; }
        
        public string OutputDirectory { get; set; }
        public string IntermediateDirectory { get; set; }
        
        public BuildConfiguration BuildType { get; set; }
        public ArchitectureMode Architecture { get; set; }
        public NativeIntermediateMode NativeMode { get; set; }
        public OSMode OS { get; set; }
        
        public List<string> ReferencePaths { get; set; }
        
        // Optional Customization Points (Can be null)
        public string IlcArgs { get; set; }
        public List<string> LinkLibPaths { get; set; }
        
        // Required Customization Points (Must have default)
        public string AppDepSDKPath { get; set; }
        public string IlcPath { get; set; }

        public NativeCompileSettings()
        {
            LinkLibPaths = new List<string>();
            ReferencePaths = new List<string>();
        }   

        public string DetermineFinalOutputPath()
        {
            var outputDirectory = this.OutputDirectory;
            
            var filename = Path.GetFileNameWithoutExtension(this.InputManagedAssemblyPath);
        
            var outFile = Path.Combine(outputDirectory, filename + Constants.ExeSuffix);
            
            return outFile;
        }
    }


}