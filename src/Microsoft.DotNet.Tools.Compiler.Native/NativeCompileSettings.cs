using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
	public class NativeCompileSettings
	{
		public string LogPath { get; set; }
		public string InputManagedAssemblyPath { get; set; }
		
		public string OutputDirectory { get; set; }
		public string IntermediateDirectory { get; set; }
		
		public BuildConfiguration BuildType { get; set; }
		public ArchitectureMode Architecture { get; set; }
		public NativeIntermediateMode NativeMode { get; set; }
		public OSMode OS { get; set; }
		
		public List<string> ReferencePaths { get; set; }
		
		// Optional Customization Points (Can be null)
		public string IlcArgs { get; set; }
		public List<string> LinkLibPaths { get; set; }
		
		// Required Customization Points (Must have default)
		public string AppDepSDKPath { get; set; }
		public string IlcPath { get; set; }
		public string RuntimeLibPath { get; set; }

        public NativeCompileSettings()
        {
            LinkLibPaths = new List<string>();
            ReferencePaths = new List<string>();
        }
		
	}
	
	public enum NativeIntermediateMode 
	{
		cpp,
		ryujit,
		custom
	}
	
	public enum ArchitectureMode
	{
		x86,
		x64
	}
	
	public enum OSMode
	{
		Linux,
		Windows,
		Mac
	}
	
	public enum BuildConfiguration
	{
		debug,
		release
	}

}