using System.Collections.Generic;

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
        public string IlcArgs { get; set; }
        public IEnumerable<string> LinkLibPaths { get; set; }
        public string AppDepSDKPath { get; set; }
        public string IlcPath { get; set; }

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
                config.IlcPath = IlcPath;
            }

            if (!string.IsNullOrEmpty(LogPath))
            {
                config.LogPath = LogPath;
            }

            if (!string.IsNullOrEmpty(IlcArgs))
            {
                config.IlcArgs = IlcArgs;
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
