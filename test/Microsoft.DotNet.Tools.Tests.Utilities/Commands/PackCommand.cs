// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class PackCommand : TestCommand
    {
        private string _projectPath;
        private string _outputDirectory;
        private string _tempOutputDirectory;
        private string _configuration;
        private string _versionSuffix;

        private string OutputOption
        {
            get
            {
                return _outputDirectory == string.Empty ?
                                           "" :
                                           $"-o \"{_outputDirectory}\"";
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

        private string VersionSuffixOption
        {
            get
            {
                return _versionSuffix == string.Empty ?
                                           "" :
                                           $"--version-suffix {_versionSuffix}";
            }
        }

        public PackCommand(
            string projectPath, 
            string output="", 
            string tempOutput="", 
            string configuration="", 
            string versionSuffix="")
            : base("dotnet")
        {
            _projectPath = projectPath;
            _outputDirectory = output;
            _tempOutputDirectory = tempOutput;
            _configuration = configuration;
            _versionSuffix = versionSuffix;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"pack {BuildArgs()} {args}";
            return base.Execute(args);
        }

        private string BuildArgs()
        {
            return $"{_projectPath} {OutputOption} {TempOutputOption} {ConfigurationOption} {VersionSuffixOption}";
        }
    }
}
