using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    internal class ArgValues
    {
        public string LogPath { get; set; }
        public string InputManagedAssemblyPath { get; set; }
        public string OutputDirectory { get; set; }
        public string IntermediateDirectory { get; set; }
        public BuildConfiguration? BuildConfiguration { get; set; }
        public ArchitectureMode Architecture { get; set; }
        public NativeIntermediateMode? NativeMode { get; set; }
        public IEnumerable<string> ReferencePaths { get; set; }
        public IEnumerable<string> IlcArgs { get; set; }
        public IEnumerable<string> LinkLibPaths { get; set; }
        public string AppDepSDKPath { get; set; }
        public string IlcPath { get; set; }
        public string IlcSdkPath { get; set; }
        public string CppCompilerFlags { get; set; }

        public bool IsHelp { get; set; }
        public int ReturnCode { get; set; }

        public NativeCompileSettings GetNativeCompileSettings()
        {
            var config = NativeCompileSettings.Default;

            config.InputManagedAssemblyPath = InputManagedAssemblyPath;
            config.Architecture = Architecture;

            if (BuildConfiguration.HasValue)
            {
                config.BuildType = BuildConfiguration.Value;
            }

            if (!string.IsNullOrEmpty(OutputDirectory))
            {
                config.OutputDirectory = OutputDirectory;
            }

            if (!string.IsNullOrEmpty(IntermediateDirectory))
            {
                config.IntermediateDirectory = IntermediateDirectory;
            }

            if (NativeMode.HasValue)
            {
                config.NativeMode = NativeMode.Value;
            }

            if (!string.IsNullOrEmpty(AppDepSDKPath))
            {
                config.AppDepSDKPath = AppDepSDKPath;
            }

            if (!string.IsNullOrEmpty(IlcPath))
            {
                // We want a directory path. If the user gave us the exact path to the executable
                // then we can be helpful and convert that to the directory rather than forcing
                // the command to be re-typed.
                string ilcDir = IlcPath;
                if (File.Exists(IlcPath) && !Directory.Exists(IlcPath))
                {
                    string potentialIlcDir = Path.GetDirectoryName(IlcPath);
                    if (Directory.Exists(potentialIlcDir))
                    {
                        ilcDir = potentialIlcDir;
                    }
                }
                config.IlcPath = ilcDir;
                config.IlcSdkPath = ilcDir;
            }

            if (!string.IsNullOrEmpty(IlcSdkPath))
            {
                config.IlcSdkPath = IlcSdkPath;
            }

            // Get the directory name to ensure there are no trailing slashes as they may conflict
            // with the terminating "  we suffix to account for paths with spaces in them.
            char[] charsToTrim = {'\\', '/'};
            config.IlcSdkPath = config.IlcSdkPath.TrimEnd(charsToTrim);
            
            if (!string.IsNullOrEmpty(LogPath))
            {
                config.LogPath = LogPath;
            }

            if (IlcArgs != null)
            {
                config.IlcArgs = IlcArgs;
            }

            if (!string.IsNullOrWhiteSpace(CppCompilerFlags))
            {
                config.CppCompilerFlags = CppCompilerFlags;
            }

            foreach (var reference in ReferencePaths)
            {
                config.AddReference(reference);
            }

            foreach (var lib in LinkLibPaths)
            {
                config.AddLinkLibPath(lib);
            }

            return config;
        }
    }
}
