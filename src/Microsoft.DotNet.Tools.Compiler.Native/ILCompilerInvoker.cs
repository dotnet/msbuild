using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class ILCompilerInvoker
    {
        private readonly string ExecutableName = "corerun" + Constants.ExeSuffix;
        private readonly string ILCompiler = "ilc.exe";
        private IEnumerable<string> Args;
        private NativeCompileSettings config;

        private static readonly Dictionary<NativeIntermediateMode, string> ModeOutputExtensionMap = new Dictionary<NativeIntermediateMode, string>
        {
            { NativeIntermediateMode.cpp, ".cpp" },
            { NativeIntermediateMode.ryujit, ".obj" }
        };
        
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

            argsList.Add($"{managedPath}");
            
            // Input File 
            var inputFilePath = config.InputManagedAssemblyPath;
            argsList.Add($"{inputFilePath}");
            
            // System.Private.* References
            var coreLibsPath = Path.Combine(config.IlcSdkPath, "sdk");
            foreach (var reference in Directory.EnumerateFiles(coreLibsPath, "*.dll"))
            {
                argsList.Add($"-r");
                argsList.Add($"{reference}");
            }
            
            // AppDep References
            foreach (var reference in config.ReferencePaths)
            {
                argsList.Add($"-r");
                argsList.Add($"{reference}");
            }
            
            // Set Output DetermineOutFile
            var outFile = DetermineOutputFile(config);
            argsList.Add($"-out");
            argsList.Add($"{outFile}");
            
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
                        
            Args = argsList;
        }

        public int Invoke()
        {
            var executablePath = Path.Combine(config.IlcPath, ExecutableName);
            
            var result = Command.Create(executablePath, Args)
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
