using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class LinuxCppCompileStep : IPlatformNativeStep
    {
        private const string CompilerName = "clang-3.5";
        private const string InputExtension = ".cpp";

        // TODO: debug/release support
        private readonly string [] _cLibsFlags = { "-lm", "-ldl"};
        private readonly string [] _cflags = { "-g", "-lstdc++", "-lrt", "-Wno-invalid-offsetof", "-lpthread"};

        public IEnumerable<string> CompilerArgs { get; set; }

        private readonly string[] _ilcSdkLibs = 
            {
                "libbootstrappercpp.a",
                "libPortableRuntime.a",
                "libSystem.Private.CoreLib.Native.a"
            };

        private readonly string[] _appdeplibs = 
            {
                "System.Native.a"
            };

        public LinuxCppCompileStep(NativeCompileSettings config)
        {
            InitializeArgs(config);
        }

        public int Invoke()
        {
            var result = InvokeCompiler();
            if (result != 0)
            {
                Reporter.Error.WriteLine("Compilation of intermediate files failed.");
            }

            return result;
        }

        public bool CheckPreReqs()
        {
            // TODO check for clang
            return true;
        }

        private void InitializeArgs(NativeCompileSettings config)
        {
            var argsList = new List<string>();
            
            // Flags
            argsList.AddRange(_cflags);

            var ilcSdkIncPath = Path.Combine(config.IlcSdkPath, "inc");
            argsList.Add("-I");
            argsList.Add($"{ilcSdkIncPath}");

            // Input File
            var inCppFile = DetermineInFile(config);
            argsList.Add(inCppFile);

            // Pass the optional native compiler flags if specified
            if (!string.IsNullOrWhiteSpace(config.CppCompilerFlags))
            {
                argsList.Add(config.CppCompilerFlags);
            }
            
            // ILC SDK Libs
            var ilcSdkLibPath = Path.Combine(config.IlcSdkPath, "sdk");
            argsList.AddRange(_ilcSdkLibs.Select(lib => Path.Combine(ilcSdkLibPath, lib)));

            // AppDep Libs
            var baseAppDeplibPath = Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04/x64");
            argsList.AddRange(_appdeplibs.Select(lib => Path.Combine(baseAppDeplibPath, lib)));

            argsList.AddRange(_cLibsFlags);
            
            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o");
            argsList.Add($"{libOut}");

            CompilerArgs = argsList;
        }

        private int InvokeCompiler()
        {
            var result = Command.Create(CompilerName, CompilerArgs)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            return result.ExitCode;
        }

        private string DetermineInFile(NativeCompileSettings config)
        {
            var intermediateDirectory = config.IntermediateDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var infile = Path.Combine(intermediateDirectory, filename + InputExtension);

            return infile;
        }

        public string DetermineOutputFile(NativeCompileSettings config)
        {
            var intermediateDirectory = config.OutputDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var outfile = Path.Combine(intermediateDirectory, filename);

            return outfile;
        }
    }
}
