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
    public class WindowsLinkStep : IPlatformNativeStep
    {
        private readonly string LinkerName = "link.exe";
        private readonly string LinkerOutputExtension = ".exe";
        private readonly string VSBin = "..\\..\\VC\\bin\\amd64";

        private readonly string InputExtension = ".obj";

        private static readonly Dictionary<BuildConfiguration, string> ConfigurationLinkerOptionsMap = new Dictionary<BuildConfiguration, string>
        {
            { BuildConfiguration.debug, "/NOLOGO /ERRORREPORT:PROMPT /MANIFEST /MANIFESTUAC:\"level='asInvoker' uiAccess='false'\" /manifest:embed /Debug /SUBSYSTEM:CONSOLE /TLBID:1 /DYNAMICBASE /NXCOMPAT" },
            { BuildConfiguration.release, "/NOLOGO /ERRORREPORT:PROMPT /INCREMENTAL:NO /OPT:REF /OPT:ICF /LTCG:incremental /MANIFEST /MANIFESTUAC:\"level='asInvoker' uiAccess='false'\" /manifest:embed /Debug /SUBSYSTEM:CONSOLE /TLBID:1 /DYNAMICBASE /NXCOMPAT" }
        };

        private static readonly Dictionary<NativeIntermediateMode, string[]> ModeLibMap = new Dictionary<NativeIntermediateMode, string[]>
        {
            { NativeIntermediateMode.cpp, new string[] { "PortableRuntime.lib", "bootstrappercpp.lib" } },
            { NativeIntermediateMode.ryujit, new string[] { "Runtime.lib", "bootstrapper.lib" } }
        };

        private static readonly string[] ConstantLinkLibs = new string[]
        {
            "kernel32.lib",
            "user32.lib",
            "gdi32.lib",
            "winspool.lib",
            "comdlg32.lib",
            "advapi32.lib",
            "shell32.lib",
            "ole32.lib",
            "oleaut32.lib",
            "uuid.lib",
            "odbc32.lib",
            "odbccp32.lib"
        };

        private static readonly Dictionary<BuildConfiguration, string[]> ConfigurationLinkLibMap = new Dictionary<BuildConfiguration, string[]>()
        {
            { BuildConfiguration.debug , new string[] { "msvcrtd.lib" } },
            { BuildConfiguration.release , new string[] { "msvcrt.lib" } }
        };
        
        private string ArgStr { get; set; }
        private NativeCompileSettings config;
        
        public WindowsLinkStep(NativeCompileSettings config)
        {
            this.config = config;
            InitializeArgs(config);
        }
        
        public int Invoke()
        {
            var result = WindowsCommon.SetVCVars();
            if (result != 0)
            {
                Reporter.Error.WriteLine("vcvarsall.bat invocation failed.");
                return result;
            }
            
            result = InvokeLinker();
            if (result != 0)
            {
                Reporter.Error.WriteLine("Linking of intermediate files failed.");
            }
            return result;
        }
        
        public bool CheckPreReqs()
        {
            var vcInstallDir = Environment.GetEnvironmentVariable("VS140COMNTOOLS");
            return !string.IsNullOrEmpty(vcInstallDir);
        }
        
        private void InitializeArgs(NativeCompileSettings config)
        {
            var argsList = new List<string>();
            
            // Configuration Based Linker Options 
            argsList.Add(ConfigurationLinkerOptionsMap[config.BuildType]);
            
            //Output
            var outFile = DetermineOutputFile(config);
            argsList.Add($"/out:\"{outFile}\"");
            
            // Constant Libs
            argsList.Add(string.Join(" ", ConstantLinkLibs));

            // SDK Libs
            var SDKLibs = ModeLibMap[config.NativeMode];
            foreach (var lib in SDKLibs)
            {
                var sdkLibPath = Path.Combine(config.IlcPath, lib);
                argsList.Add($"\"{sdkLibPath}\"");
            }

            // Configuration Based Libs
            var configLibs = ConfigurationLinkLibMap[config.BuildType];
            foreach (var lib in configLibs)
            {
                argsList.Add($"\"{lib}\"");
            }

            // Link Libs
            foreach(var path in config.LinkLibPaths){
                argsList.Add($"\"{path}\"");
            }
            
            //arch
            argsList.Add($"/MACHINE:{config.Architecture}");

            //Input Obj file
            var inputFile = DetermineInputFile(config);
            argsList.Add($"\"{inputFile}\"");
            
            this.ArgStr = string.Join(" ", argsList);
        }
        
        private int InvokeLinker()
        {
            var vcInstallDir = Environment.GetEnvironmentVariable("VS140COMNTOOLS");
            var linkerPath = Path.Combine(vcInstallDir, VSBin, LinkerName);
            
            var result = Command.Create(linkerPath, ArgStr)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();
            return result.ExitCode;
        }
        
        public string DetermineOutputFile(NativeCompileSettings config)
        {
            var outputDirectory = config.OutputDirectory;
            
            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);
        
            var outFile = Path.Combine(outputDirectory, filename + LinkerOutputExtension);
            
            return outFile;
        }

        private string DetermineInputFile(NativeCompileSettings config)
        {
            var intermediateDirectory = config.IntermediateDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var infile = Path.Combine(intermediateDirectory, filename + InputExtension);

            return infile;
        }
        
    }
}
