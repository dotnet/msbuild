using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class MacRyuJitCompileStep : IPlatformNativeStep
    {
        private const string CompilerName = "clang";
        private const string InputExtension = ".obj";

        private const string CompilerOutputExtension = "";

        private IEnumerable<string> CompilerArgs;

        // TODO: debug/release support
        private readonly string [] _cflags = { "-g", "-lstdc++", "-Wno-invalid-offsetof", "-lpthread", "-ldl", "-lm", "-liconv" };

        private readonly string[] _ilcSdkLibs = 
            {
                "libbootstrapper.a",
                "libRuntime.a",
                "libSystem.Private.CoreLib.Native.a"
            };

        private readonly string[] appdeplibs = 
            {
                "System.Native.a"
            };

        public MacRyuJitCompileStep(NativeCompileSettings config)
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
            
            // Pass the optional native compiler flags if specified
            if (!string.IsNullOrWhiteSpace(config.CppCompilerFlags))
            {
                argsList.Add(config.CppCompilerFlags);
            }
            
            // Input File
            var inLibFile = DetermineInFile(config);
            argsList.Add(inLibFile);

            // ILC SDK Libs
            var ilcSdkLibPath = Path.Combine(config.IlcSdkPath, "sdk");
            argsList.AddRange(_ilcSdkLibs.Select(lib => Path.Combine(ilcSdkLibPath, lib)));

            // Optional linker script
            var linkerScriptFile = Path.Combine(ilcSdkLibPath, "linkerscript");
            if (File.Exists(linkerScriptFile))
            {
                argsList.Add(linkerScriptFile);
            }

            // AppDep Libs
            var baseAppDepLibPath = Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10", config.Architecture.ToString());
            argsList.AddRange(appdeplibs.Select(lib => Path.Combine(baseAppDepLibPath, lib)));

            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o");
            argsList.Add($"{libOut}");

            this.CompilerArgs = argsList;
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

            var outfile = Path.Combine(intermediateDirectory, filename + CompilerOutputExtension);

            return outfile;
        }
    }
}
