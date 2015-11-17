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
        private readonly string cflags = "-lstdc++ -lpthread -ldl -lm";

        private readonly string[] libs = new string[]
        {
            "libbootstrapper.a",
            "libRuntime.a",
            "libPortableRuntime.a",
            "libSystem.Private.CoreLib.Native.a",
            "System.Native.so"
        };


        private string CompilerArgStr { get; set; }

        public LinuxRyuJitCompileStep(Config config)
        {
            InitializeArgs(config);
        }

        public int Invoke(Config config)
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

        private void InitializeArgs(Config config)
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
                var libPath = Path.Combine(config.RuntimeLibPath, lib);
                argsList.Add(libPath);
            }

            // Output
            var libOut = DetermineOutputFile(config);
            argsList.Add($"-o \"{libOut}\"");

            // Add Stubs
            argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk/ubuntu.14.04/lxstubs.cpp"));

            this.CompilerArgStr = string.Join(" ", argsList);
        }

        private int InvokeCompiler(Config config)
        {
            var result = Command.Create(CompilerName, CompilerArgStr)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            // Needs System.Native.so in output
            var sharedLibPath = Path.Combine(config.ILToNativePath, "System.Native.so");
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

        private string DetermineInFile(Config config)
        {
            var intermediateDirectory = config.IntermediateDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var infile = Path.Combine(intermediateDirectory, filename + InputExtension);

            return infile;
        }

        public string DetermineOutputFile(Config config)
        {
            var intermediateDirectory = config.OutputDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var outfile = Path.Combine(intermediateDirectory, filename + CompilerOutputExtension);

            return outfile;
        }

        public bool RequiresLinkStep()
        {
            return false;
        }

    }
}