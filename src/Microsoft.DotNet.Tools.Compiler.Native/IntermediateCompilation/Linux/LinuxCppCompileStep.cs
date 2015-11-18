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
    public class LinuxCppCompileStep : IPlatformNativeStep
    {
        private readonly string CompilerName = "clang-3.5";
        private readonly string InputExtension = ".cpp";

        // TODO: debug/release support
        private readonly string cLibsFlags = "-lm -ldl";
        private readonly string cflags = "-g -lstdc++ -lrt -Wno-invalid-offsetof -pthread";

        private readonly string[] libs = new string[]
        {
            "libbootstrappercpp.a",
            "libPortableRuntime.a",
            "libSystem.Private.CoreLib.Native.a",
            "System.Native.so"
        };

        
        private string CompilerArgStr { get; set; }

        public LinuxCppCompileStep(NativeCompileSettings config)
        {
            InitializeArgs(config);
        }

        public int Invoke(NativeCompileSettings config)
        {
            var result = InvokeCompiler(config);
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

            // Add Includes
            argsList.Add("-I");
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04"));

            argsList.Add("-I");
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk"));


            // Input File
            var inCppFile = DetermineInFile(config);
            argsList.Add(inCppFile);

            // Add Stubs
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04/lxstubs.cpp"));


            // Libs
            foreach (var lib in libs)
            {
                var libPath = Path.Combine(config.RuntimeLibPath, lib);
                argsList.Add(libPath);
            }
            argsList.Add(cLibsFlags);
            
            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o \"{libOut}\"");



            this.CompilerArgStr = string.Join(" ", argsList);
        }

        private int InvokeCompiler(NativeCompileSettings config)
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

            var outfile = Path.Combine(intermediateDirectory, filename);

            return outfile;
        }
    }
}