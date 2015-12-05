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
    public class MacRyuJitCompileStep : IPlatformNativeStep
    {
        private readonly string CompilerName = "clang";
        private readonly string InputExtension = ".obj";

        private readonly string CompilerOutputExtension = "";

        // TODO: debug/release support
        private readonly string cflags = "-g -lstdc++ -Wno-invalid-offsetof -pthread -ldl -lm -liconv";

        private readonly string[] libs = new string[]
        {
            "libbootstrapper.a",
            "libRuntime.a",
            "libSystem.Private.CoreLib.Native.a"
        };

        private readonly string[] appdeplibs = new string[]
        {
            "libSystem.Native.a"
        };


        private string CompilerArgStr { get; set; }
        private NativeCompileSettings config;

        public MacRyuJitCompileStep(NativeCompileSettings config)
        {
            this.config = config;
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
            argsList.Add(cflags);
            
            // Add Stubs
            argsList.Add("-I "+Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10"));
            argsList.Add("-I "+Path.Combine(config.AppDepSDKPath, "CPPSdk"));
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10/osxstubs.cpp"));

            // Input File
            var inLibFile = DetermineInFile(config);
            argsList.Add("-Xlinker "+inLibFile);

            // Libs
            foreach (var lib in libs)
            {
                var libPath = Path.Combine(config.IlcPath, lib);
                argsList.Add("-Xlinker "+libPath);
            }

            // AppDep Libs
            var baseAppDepLibPath = Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10", config.Architecture.ToString());
            foreach (var lib in appdeplibs)
            {
                var appDepLibPath = Path.Combine(baseAppDepLibPath, lib);
                argsList.Add("-Xlinker "+appDepLibPath);
            }

            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o \"{libOut}\"");

            this.CompilerArgStr = string.Join(" ", argsList);
        }

        private int InvokeCompiler()
        {
            var result = Command.Create(CompilerName, CompilerArgStr)
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
