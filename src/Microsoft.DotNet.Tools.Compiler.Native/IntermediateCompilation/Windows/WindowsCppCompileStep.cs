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
	public class WindowsCppCompileStep : IPlatformNativeStep
	{
        //TODO support x86
        private readonly string CompilerName = "cl.exe";
		
        private readonly string VSBin = "..\\..\\VC\\bin\\x86_amd64";
		private readonly string InputExtension = ".cpp";
		
		private readonly string CompilerOutputExtension = ".obj";
		
		private static readonly Dictionary<BuildConfiguration, string> ConfigurationCompilerOptionsMap = new Dictionary<BuildConfiguration, string>
		{
			{ BuildConfiguration.debug, "/ZI /nologo /W3 /WX- /sdl /Od /D CPPCODEGEN /D WIN32 /D _DEBUG /D _CONSOLE /D _LIB /D _UNICODE /D UNICODE /Gm /EHsc /RTC1 /MDd /GS /fp:precise /Zc:wchar_t /Zc:forScope /Zc:inline /Gd /TP /wd4477 /errorReport:prompt" },
			{ BuildConfiguration.release, "/Zi /nologo /W3 /WX- /sdl /O2 /Oi /GL /D CPPCODEGEN /D WIN32 /D NDEBUG /D _CONSOLE /D _LIB /D _UNICODE /D UNICODE /Gm- /EHsc /MD /GS /Gy /fp:precise /Zc:wchar_t /Zc:forScope /Zc:inline /Gd /TP /wd4477 /errorReport:prompt" }
		};
		
		private string CompilerArgStr { get; set; }
		
		public WindowsCppCompileStep(Config config)
		{
			InitializeArgs(config);
		}
		
		public int Invoke(Config config)
		{
			var result = WindowsCommon.SetVCVars();
			if (result != 0)
			{
				Reporter.Error.WriteLine("vcvarsall.bat invocation failed.");
				return result;
			}
			
			result = InvokeCompiler(config);
			if (result != 0)
			{
				Reporter.Error.WriteLine("Compilation of intermediate files failed.");
			}
			
			return result;
		}
		
		public bool CheckPreReqs()
		{
            var vcInstallDir = Environment.GetEnvironmentVariable("VS140COMNTOOLS");
            return !string.IsNullOrEmpty(vcInstallDir);
		}
		
		private void InitializeArgs(Config config)
		{
			var argsList = new List<string>();
			
			// Use a Custom Link Step
			argsList.Add("/c");
			
			// Add Includes
			argsList.Add("/I");
			argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk\\Windows_NT"));
			
			argsList.Add("/I");
			argsList.Add(Path.Combine(config.AppDepSDKPath, "CPPSdk"));
			
			// Configuration Based Compiler Options 
			argsList.Add(ConfigurationCompilerOptionsMap[config.BuildType]);
			
			// Output
			var objOut = DetermineOutputFile(config);
			argsList.Add($"/Fo\"{objOut}\"");
			
			// Input File
			var inCppFile = DetermineInFile(config);
			argsList.Add(inCppFile);
			
			this.CompilerArgStr = string.Join(" ", argsList);
		}
		
		private int InvokeCompiler(Config config)
		{
			var vcInstallDir = Environment.GetEnvironmentVariable("VS140COMNTOOLS");
			var compilerPath = Path.Combine(vcInstallDir, VSBin, CompilerName);
			
			var result = Command.Create(compilerPath, CompilerArgStr)
				.ForwardStdErr()
				.ForwardStdOut()
				.Execute();
				
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
            var intermediateDirectory = config.IntermediateDirectory;

            var filename = Path.GetFileNameWithoutExtension(config.InputManagedAssemblyPath);

            var outfile = Path.Combine(intermediateDirectory, filename + CompilerOutputExtension);
            
            return outfile;
		}
		
	}
}