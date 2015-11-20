using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class NativeCompileSettings
    {
        private const BuildConfiguration DefaultBuiltType = BuildConfiguration.debug;
        private const NativeIntermediateMode DefaultNativeModel = NativeIntermediateMode.ryujit;
        private const ArchitectureMode DefaultArchitectureMode = ArchitectureMode.x64;

        public string LogPath { get; set; }
        public string InputManagedAssemblyPath { get; set; }
        
        public string OutputDirectory { get; set; }
        public string IntermediateDirectory { get; set; }

        public BuildConfiguration BuildType { get; set; }

        public string BuildTypeString
        {
            set
            {
                try
                {
                    BuildType = EnumExtensions.Parse<BuildConfiguration>(value.ToLower());
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid Configuration Option.");
                }
            }
        }

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

        private NativeCompileSettings()
        {
            LinkLibPaths = new List<string>();
            ReferencePaths = new List<string>();
            
            IlcPath = AppContext.BaseDirectory;
            Architecture = DefaultArchitectureMode;
            BuildType = DefaultBuiltType;
            NativeMode = DefaultNativeModel;
            AppDepSDKPath = Path.Combine(AppContext.BaseDirectory, "appdepsdk");

            ReferencePaths.Add(Path.Combine(AppDepSDKPath, "*.dll"));
        }

        public static NativeCompileSettings Default
        {
            get
            {
                var nativeCompileSettings = new NativeCompileSettings
                {                                        
                    OS = RuntimeInformationExtensions.GetCurrentOS()
                };

                nativeCompileSettings.SetDefaultOutputDirectory();
                nativeCompileSettings.SetDefaultIntermediateDirectory();

                return nativeCompileSettings;
            }
        }

        public string DetermineFinalOutputPath()
        {
            var outputDirectory = OutputDirectory;
            
            var filename = Path.GetFileNameWithoutExtension(InputManagedAssemblyPath);
        
            var outFile = Path.Combine(outputDirectory, filename + Constants.ExeSuffix);
            
            return outFile;
        }        

        private void SetDefaultOutputDirectory()
        {
            OutputDirectory = GetOutputDirectory(Constants.BinDirectoryName);
        }

        private void SetDefaultIntermediateDirectory()
        {
            IntermediateDirectory = GetOutputDirectory(Constants.ObjDirectoryName);
        }

        private string GetOutputDirectory(string beginsWith)
        {
            var dir = Path.Combine(beginsWith, Architecture.ToString(), BuildType.ToString(), "native");

            return Path.GetFullPath(dir);
        }
    }
}