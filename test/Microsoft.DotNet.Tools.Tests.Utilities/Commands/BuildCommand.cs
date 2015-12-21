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
        private string _projectPath;
        private string _outputDirectory;
        private string _tempOutputDirectory;
        private string _configuration;
        private bool _noHost;
        private bool _native;
        private string _architecture;
        private string _ilcArgs;
        private string _ilcPath;
        private string _appDepSDKPath;
        private bool _nativeCppMode;
        private string _cppCompilerFlags;
        private bool _buildProfile;
        private bool _forceIncrementalUnsafe;

        private string OutputOption
        {
            get
            {
                return _outputDirectory == string.Empty ?
                                           "" :
                                           $"-o {_outputDirectory}";
            }
        }

        private string TempOutputOption
        {
            get
            {
                return _tempOutputDirectory == string.Empty ?
                                           "" :
                                           $"-t {_tempOutputDirectory}";
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

        private string ForceIncrementalUnsafe
        {
            get
            {
                return _forceIncrementalUnsafe ?
                    "--force-incremental-unsafe" :
                    "";
            }
        }

        public BuildCommand(
            string projectPath,
            string output="",
            string tempOutput="",
            string configuration="",
            bool noHost=false,
            bool native=false,
            string architecture="",
            string ilcArgs="",
            string ilcPath="",
            string appDepSDKPath="",
            bool nativeCppMode=false,
            string cppCompilerFlags="",
            bool buildProfile=true,
            bool forceIncrementalUnsafe=false
            )
            : base("dotnet")
        {

            _projectPath = projectPath;
            _project = ProjectReader.GetProject(projectPath);

            _outputDirectory = output;
            _tempOutputDirectory = tempOutput;
            _configuration = configuration;
            _noHost = noHost;
            _native = native;
            _architecture = architecture;
            _ilcArgs = ilcArgs;
            _ilcPath = ilcPath;
            _appDepSDKPath = appDepSDKPath;
            _nativeCppMode = nativeCppMode;
            _cppCompilerFlags = cppCompilerFlags;
            _buildProfile = buildProfile;
            _forceIncrementalUnsafe = forceIncrementalUnsafe;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"--verbose build {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"build {BuildArgs()} {args}";
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
            return $"{BuildProfile} {ForceIncrementalUnsafe} {_projectPath} {OutputOption} {TempOutputOption} {ConfigurationOption} {NoHostOption} {NativeOption} {ArchitectureOption} {IlcArgsOption} {IlcPathOption} {AppDepSDKPathOption} {NativeCppModeOption} {CppCompilerFlagsOption}";
        }
    }
}
