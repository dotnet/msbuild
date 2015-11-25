using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class ILCompilerInvoker
    {
        private readonly string ExecutableName = "corerun" + Constants.ExeSuffix;
        private readonly string ILCompiler = "ilc.exe";

        private static readonly Dictionary<NativeIntermediateMode, string> ModeOutputExtensionMap = new Dictionary<NativeIntermediateMode, string>
        {
            { NativeIntermediateMode.cpp, ".cpp" },
            { NativeIntermediateMode.ryujit, ".obj" }
        };

        private static readonly Dictionary<OSMode, string> OSCoreLibNameMap = new Dictionary<OSMode, string>()
        {
            {OSMode.Windows, "System.Private.CoreLib.dll" },
            {OSMode.Linux, "System.Private.Corelib.dll" },
            {OSMode.Mac, "System.Private.Corelib.dll" },
        };

        
        private string ArgStr { get; set; }
        private NativeCompileSettings config;
        
        public ILCompilerInvoker(NativeCompileSettings config)
        {
            this.config = config;
            InitializeArgs(config);
        }
        
        private void InitializeArgs(NativeCompileSettings config)
        {
            var argsList = new List<string>();

            var managedPath = Path.Combine(config.IlcPath, ILCompiler);
            if (!File.Exists(managedPath))
            {
                throw new FileNotFoundException("Unable to find ILCompiler at " + managedPath);
            }

            argsList.Add($"\"{managedPath}\"");
            
            // Input File 
            var inputFilePath = config.InputManagedAssemblyPath;
            argsList.Add($"\"{inputFilePath}\"");
            
            // System.Private.CoreLib Reference
            var coreLibPath = Path.Combine(config.IlcPath, OSCoreLibNameMap[config.OS]);
            argsList.Add($"-r \"{coreLibPath}\"");
            
            // Dependency References
            foreach (var reference in config.ReferencePaths)
            {
                argsList.Add($"-r \"{reference}\"");
            }
            
            // Set Output DetermineOutFile
            var outFile = DetermineOutputFile(config);
            argsList.Add($"-out \"{outFile}\"");
            
            // Add Mode Flag TODO
            if (config.NativeMode == NativeIntermediateMode.cpp)
            {
                argsList.Add("-cpp");
            }
            
            // Custom Ilc Args support
            if (! string.IsNullOrEmpty(config.IlcArgs))
            {
                argsList.Add(config.IlcArgs);
            }
                        
            this.ArgStr = string.Join(" ", argsList);
        }
        
        public int Invoke()
        {
            var executablePath = Path.Combine(config.IlcPath, ExecutableName);
            
            var result = Command.Create(executablePath, ArgStr)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();
            
            return result.ExitCode;
        }
        
        public string DetermineOutputFile(NativeCompileSettings config)
        {
            var intermediateDirectory = config.IntermediateDirectory;
            
            var extension = ModeOutputExtensionMap[config.NativeMode];
            
            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);
        
            var outFile = Path.Combine(intermediateDirectory, filename + extension);
            
            return outFile;
        }
        
    }
}
