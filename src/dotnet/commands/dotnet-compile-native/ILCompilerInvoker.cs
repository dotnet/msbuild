using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class ILCompilerInvoker
    {
        private static readonly string HostExeName = "corerun" + Constants.ExeSuffix;
        private static readonly string ILCompiler = "ilc.exe";

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

            // Input File 
            var inputFilePath = config.InputManagedAssemblyPath;
            argsList.Add($"{inputFilePath}");
            
            // System.Private.* References
            var coreLibsPath = Path.Combine(config.IlcSdkPath, "sdk");
            foreach (var reference in Directory.EnumerateFiles(coreLibsPath, "*.dll"))
            {
                argsList.Add($"-r:{reference}");
            }
            
            // AppDep References
            foreach (var reference in config.ReferencePaths)
            {
                argsList.Add($"-r:{reference}");
            }
            
            // Set Output DetermineOutFile
            var outFile = DetermineOutputFile(config);
            argsList.Add($"-o:{outFile}");
            
            // Add Mode Flag TODO
            if (config.NativeMode == NativeIntermediateMode.cpp)
            {
                argsList.Add("--cpp");
            }
            
            // Custom Ilc Args support
            foreach (var ilcArg in config.IlcArgs)
            {
                argsList.Add(ilcArg);
            }
                        
            Args = argsList;
        }

        public int Invoke()
        {
            // Check if ILCompiler is present
            var ilcExePath = Path.Combine(config.IlcPath, ILCompiler);
            if (!File.Exists(ilcExePath))
            {
                throw new FileNotFoundException("Unable to find ILCompiler at " + ilcExePath);
            }

            // Write the response file
            var intermediateDirectory = config.IntermediateDirectory;
            var rsp = Path.Combine(intermediateDirectory, "dotnet-compile-native-ilc.rsp");
            File.WriteAllLines(rsp, Args, Encoding.UTF8);

            var hostPath = Path.Combine(config.IlcPath, HostExeName);
            var result = Command.Create(hostPath, new string[] { ilcExePath, "@" + $"{rsp}" })
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
