// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class BuildCommand : TestCommand
    {
        private Project _project;
        private readonly string _projectPath;
        private readonly string _outputDirectory;
        private readonly string _buidBasePathDirectory;
        private readonly string _configuration;
        private readonly string _framework;
        private readonly string _versionSuffix;
        private readonly bool _noHost;
        private readonly bool _native;
        private readonly string _architecture;
        private readonly string _ilcArgs;
        private readonly string _ilcPath;
        private readonly string _appDepSDKPath;
        private readonly bool _nativeCppMode;
        private readonly string _cppCompilerFlags;
        private readonly bool _buildProfile;
        private readonly bool _noIncremental;
        private readonly bool _noDependencies;
        private readonly string _runtime;

        private string OutputOption
        {
            get
            {
                return _outputDirectory == string.Empty ?
                                           "" :
                                           $"-o \"{_outputDirectory}\"";
            }
        }

        private string BuildBasePathOption
        {
            get
            {
                return _buidBasePathDirectory == string.Empty ?
                                           "" :
                                           $"-b {_buidBasePathDirectory}";
            }
        }

        private string ConfigurationOption
        {
            get
            {
                return _configuration == string.Empty ?
                                           "" :
                                           $"-c {_configuration}";
            }
        }
        private string FrameworkOption
        {
            get
            {
                return _framework == string.Empty ?
                                           "" :
                                           $"--framework {_framework}";
            }
        }

        private string VersionSuffixOption
        {
            get
            {
                return _versionSuffix == string.Empty ?
                                    "" :
                                    $"--version-suffix {_versionSuffix}";
            }
        }

        private string NoHostOption
        {
            get
            {
                return _noHost ?
                        "--no-host" :
                        "";
            }
        }

        private string NativeOption
        {
            get
            {
                return _native ?
                        "--native" :
                        "";
            }
        }

        private string RuntimeOption
        {
            get
            {
                return _runtime == string.Empty ?
                    "" :
                    $"--runtime {_runtime}";
            }
        }

        private string ArchitectureOption
        {
            get
            {
                return _architecture == string.Empty ?
                                           "" :
                                           $"--arch {_architecture}";
            }
        }

        private string IlcArgsOption
        {
            get
            {
                return _ilcArgs == string.Empty ?
                                           "" :
                                           $"--ilcargs {_ilcArgs}";
            }
        }

        private string IlcPathOption
        {
            get
            {
                return _ilcPath == string.Empty ?
                                           "" :
                                           $"--ilcpath {_ilcPath}";
            }
        }

        private string AppDepSDKPathOption
        {
            get
            {
                return _appDepSDKPath == string.Empty ?
                                           "" :
                                           $"--appdepsdkpath {_appDepSDKPath}";
            }
        }

        private string NativeCppModeOption
        {
            get
            {
                return _nativeCppMode ?
                        "--cpp" :
                        "";
            }
        }

        private string CppCompilerFlagsOption
        {
            get
            {
                return _cppCompilerFlags == string.Empty ?
                                           "" :
                                           $"--cppcompilerflags {_cppCompilerFlags}";
            }
        }

        private string BuildProfile
        {
            get
            {
                return _buildProfile ?
                    "--build-profile" :
                    "";
            }
        }

        private string NoIncremental
        {
            get
            {
                return _noIncremental ?
                    "--no-incremental" :
                    "";
            }
        }

        private string NoDependencies
        {
            get
            {
                return _noDependencies ?
                    "--no-dependencies" :
                    "";
            }
        }

        public BuildCommand(
            string projectPath,
            string output="",
            string buidBasePath="",
            string configuration="",
            string framework="",
            string runtime="",
            string versionSuffix="",
            bool noHost=false,
            bool native=false,
            string architecture="",
            string ilcArgs="",
            string ilcPath="",
            string appDepSDKPath="",
            bool nativeCppMode=false,
            string cppCompilerFlags="",
            bool buildProfile=true,
            bool noIncremental=false,
            bool noDependencies=false)
            : base("dotnet")
        {
            _projectPath = projectPath;
            _project = ProjectReader.GetProject(projectPath);

            _outputDirectory = output;
            _buidBasePathDirectory = buidBasePath;
            _configuration = configuration;
            _versionSuffix = versionSuffix;
            _framework = framework;
            _runtime = runtime;
            _noHost = noHost;
            _native = native;
            _architecture = architecture;
            _ilcArgs = ilcArgs;
            _ilcPath = ilcPath;
            _appDepSDKPath = appDepSDKPath;
            _nativeCppMode = nativeCppMode;
            _cppCompilerFlags = cppCompilerFlags;
            _buildProfile = buildProfile;
            _noIncremental = noIncremental;
            _noDependencies = noDependencies;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"--verbose build {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"--verbose build {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public string GetOutputExecutableName()
        {
            var result = _project.Name;
            result += RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            return result;
        }

        private string BuildArgs()
        {
            return $"{BuildProfile} {NoDependencies} {NoIncremental} \"{_projectPath}\" {OutputOption} {BuildBasePathOption} {ConfigurationOption} {FrameworkOption} {RuntimeOption} {VersionSuffixOption} {NoHostOption} {NativeOption} {ArchitectureOption} {IlcArgsOption} {IlcPathOption} {AppDepSDKPathOption} {NativeCppModeOption} {CppCompilerFlagsOption}";
        }
    }
}
