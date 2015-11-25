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
    public class MacCppCompileStep : IPlatformNativeStep
    {
        private readonly string CompilerName = "clang";
        private readonly string InputExtension = ".cpp";

        // TODO: debug/release support
        private readonly string cflags = "-g -lstdc++ -Wno-invalid-offsetof -pthread";
        
        // Link to iconv APIs
        private readonly string libFlags = "-liconv";

        private readonly string[] libs = new string[]
        {
            "libbootstrappercpp.a",
            "libPortableRuntime.a",
            "libSystem.Private.CoreLib.Native.a"
        };

        private readonly string[] appdeplibs = new string[]
        {
            "libSystem.Native.a"
        };

        
        private string CompilerArgStr { get; set; }
        private NativeCompileSettings config;

        public MacCppCompileStep(NativeCompileSettings config)
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

            // Add Includes
            argsList.Add("-I");
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10"));

            argsList.Add("-I");
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk"));

            // Input File
            var inCppFile = DetermineInFile(config);
            argsList.Add(inCppFile);

            // Add Stubs
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10/osxstubs.cpp"));

            // Lib flags
            argsList.Add(libFlags);

            // Libs
            foreach (var lib in libs)
            {
                var libPath = Path.Combine(config.IlcPath, lib);

                // Forward the library to linked to the linker
                argsList.Add("-Xlinker");
                argsList.Add(libPath);
            }

            // AppDep Libs
            var baseAppDeplibPath = Path.Combine(config.AppDepSDKPath, "CPPSdk/osx.10.10/x64");
            foreach (var lib in appdeplibs)
            {
                var appDeplibPath = Path.Combine(baseAppDeplibPath, lib);
                argsList.Add("-Xlinker");
                argsList.Add(appDeplibPath);
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

            var outfile = Path.Combine(intermediateDirectory, filename);

            return outfile;
        }
    }
}