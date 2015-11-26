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
    public class LinuxRyuJitCompileStep : IPlatformNativeStep
    {
        private readonly string CompilerName = "clang-3.5";
        private readonly string InputExtension = ".obj";

        private readonly string CompilerOutputExtension = "";

        // TODO: debug/release support
        private readonly string cflags = "-lstdc++ -lpthread -ldl -lm -lrt";

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

        public LinuxRyuJitCompileStep(NativeCompileSettings config)
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
            
            // Input File
            var inLibFile = DetermineInFile(config);
            argsList.Add(inLibFile);

            // Libs
            foreach (var lib in libs)
            {
                var libPath = Path.Combine(config.IlcPath, lib);
                argsList.Add(libPath);
            }

            // AppDep Libs
            var baseAppDepLibPath = Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04", config.Architecture.ToString());
            foreach (var lib in appdeplibs)
            {
                var appDepLibPath = Path.Combine(baseAppDepLibPath, lib);
                argsList.Add(appDepLibPath);
            }

            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o \"{libOut}\"");

            // Add Stubs
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04/lxstubs.cpp"));

            this.CompilerArgStr = string.Join(" ", argsList);
        }

        private int InvokeCompiler()
        {
            var result = Command.Create(CompilerName, CompilerArgStr)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            // Needs System.Native.so in output
            var sharedLibPath = Path.Combine(config.IlcPath, "System.Native.so");
            var outputSharedLibPath = Path.Combine(config.OutputDirectory, "System.Native.so");
            try
            {
                File.Copy(sharedLibPath, outputSharedLibPath);
            }
            catch(Exception e)
            {
                Reporter.Error.WriteLine("Unable to copy System.Native.so to output");
            }

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
