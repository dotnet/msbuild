using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class NativeCompileSettings
    {
        private const BuildConfiguration DefaultBuiltType = BuildConfiguration.debug;
        private const NativeIntermediateMode DefaultNativeModel = NativeIntermediateMode.ryujit;
        private const ArchitectureMode DefaultArchitectureMode = ArchitectureMode.x64;

        private string _inputManagedAssemblyPath;
        private string _appDepSdkPath;
        private string _ilcPath;
        private string _ilcSdkPath;
        private string _outputDirectory;
        private string _intermediateDirectory;
        private string _logPath;
        private IEnumerable<string> _ilcArgs;
        private readonly Dictionary<string, string> _referencePaths;
        private readonly List<string> _linkLibPaths;
        private string _cppCompilerFlags;

        public string LogPath
        {
            get { return _logPath; }
            set { _logPath = Path.GetFullPath(value); }
        }

        public string InputManagedAssemblyPath
        {
            get
            {
                return _inputManagedAssemblyPath;
            }
            set
            {
                if(!File.Exists(value))
                {
                    throw new Exception($"Could not find the input managed assembly: {value}");
                }

                _inputManagedAssemblyPath = Path.GetFullPath(value);
            }
        }

        public string OutputDirectory
        {
            get
            {
                return _outputDirectory ?? GetDefaultOutputDirectory();
            }
            set
            {
                _outputDirectory = value;
            }
        }

        public string IntermediateDirectory
        {
            get
            {
                return _intermediateDirectory ?? GetDefaultIntermediateDirectory();
            }
            set
            {
                _intermediateDirectory = value;
            }
        }

        public BuildConfiguration BuildType { get; set; }
        
        public ArchitectureMode Architecture { get; set; }
        public NativeIntermediateMode NativeMode { get; set; }
        public OSMode OS { get; set; }

        public IEnumerable<string> ReferencePaths
        {
            get
            {
                return _referencePaths.Values;
            }            
        }

        // Optional Customization Points (Can be null)
        public IEnumerable<string> IlcArgs
        {
            get { return _ilcArgs; }
            set { _ilcArgs = value; }
        }
        public IEnumerable<string> LinkLibPaths => _linkLibPaths;

        // Required Customization Points (Must have default)
        public string AppDepSDKPath {
            get
            {
                return _appDepSdkPath;
            }
            set
            {
                if (!Directory.Exists(value))
                {
                    throw new Exception($"AppDepSDK Directory does not exist: {value}.");
                }

                _appDepSdkPath = value;                
            }
        }

        public string IlcPath
        {
            get
            {
                return _ilcPath;
            }
            set
            {
                if (!Directory.Exists(value))
                {
                    throw new Exception($"ILC Directory does not exist: {value}.");
                }

                _ilcPath = value;
            }
        }

        public string IlcSdkPath
        {
            get
            {
                return _ilcSdkPath;
            }
            set
            {
                if (!Directory.Exists(value))
                {
                    throw new Exception($"ILC SDK Directory does not exist: {value}.");
                }

                _ilcSdkPath = value;
            }
        }

        public string CppCompilerFlags
        {
            get
            {
                return _cppCompilerFlags;
            }
            set
            {
                _cppCompilerFlags = value;
            }
        }

        private NativeCompileSettings()
        {
            _linkLibPaths = new List<string>();
            
            IlcPath = AppContext.BaseDirectory;

            // By default, ILC SDK Path is assumed to be the same folder as ILC path
            IlcSdkPath = IlcPath;
            Architecture = DefaultArchitectureMode;
            BuildType = DefaultBuiltType;
            NativeMode = DefaultNativeModel;
            AppDepSDKPath = Path.Combine(AppContext.BaseDirectory, "appdepsdk");            
            _referencePaths = new Dictionary<string, string>();
            foreach (var file in Directory.EnumerateFiles(AppDepSDKPath, "*.dll"))
            {
                _referencePaths.Add(Path.GetFileName(file), file);
            }
        }

        public static NativeCompileSettings Default
        {
            get
            {
                var defaultNativeCompileSettings = new NativeCompileSettings
                {
                    OS = RuntimeInformationExtensions.GetCurrentOS()
                };

                return defaultNativeCompileSettings;
            }
        } 

        public string DetermineFinalOutputPath()
        {
            var outputDirectory = OutputDirectory;
            
            var filename = Path.GetFileNameWithoutExtension(InputManagedAssemblyPath);
        
            var outFile = Path.Combine(outputDirectory, filename + Constants.ExeSuffix);
            
            return outFile;
        }

        public void AddReference(string reference)
        {
            var path = Path.GetFullPath(reference);
            var simpleName = Path.GetFileName(path);
            if (!_referencePaths.ContainsKey(simpleName))
            {
                _referencePaths.Add(simpleName, path);
            }
        }

        public void AddLinkLibPath(string linkLibPath)
        {
            _linkLibPaths.Add(linkLibPath);
        }

        private string GetDefaultOutputDirectory()
        {
            return GetOutputDirectory(Constants.BinDirectoryName);
        }

        private string GetDefaultIntermediateDirectory()
        {
            return GetOutputDirectory(Constants.ObjDirectoryName);
        }

        private string GetOutputDirectory(string beginsWith)
        {
            var dir = Path.Combine(beginsWith, Architecture.ToString(), BuildType.ToString(), "native");

            return Path.GetFullPath(dir);
        }
    }
}
