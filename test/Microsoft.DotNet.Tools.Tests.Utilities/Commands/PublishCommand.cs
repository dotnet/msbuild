// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class PublishCommand : TestCommand
    {
        private Project _project;
        private string _path;
        private string _framework;
        private string _runtime;
        private string _config;
        private string _output;

        public PublishCommand(string projectPath, string framework="", string runtime="", string output="", string config="")
            : base("dotnet")
        {
            _path = projectPath;
            _project = ProjectReader.GetProject(projectPath);
            _framework = framework;
            _runtime = runtime;
            _output = output;
            _config = config;
        }

        public override CommandResult Execute(string args="")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public string ProjectName
        {
            get
            {
                return _project.Name;
            }
        }

        private string BuildRelativeOutputPath()
        {
            // lets try to build an approximate output path
            string config = string.IsNullOrEmpty(_config) ? "Debug" : _config;
            string framework = string.IsNullOrEmpty(_framework) ?
                _project.GetTargetFrameworks().First().FrameworkName.GetShortFolderName() : _framework;
            string runtime = string.IsNullOrEmpty(_runtime) ? PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier() : _runtime;
            //TODO: add runtime back as soon as it gets propagated through the various commands.
            string output = Path.Combine(config, framework);

            return output;
        }

        public DirectoryInfo GetOutputDirectory()
        {
            if (!string.IsNullOrEmpty(_output))
            {
                return new DirectoryInfo(Path.Combine(_output, BuildRelativeOutputPath()));
            }

            string output = Path.Combine(_project.ProjectDirectory, "bin", BuildRelativeOutputPath());
            return new DirectoryInfo(output);
        }

        public string GetOutputExecutable()
        {
            var result = _project.Name;
            result += RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            return result;
        }

        private string BuildArgs()
        {
            return $"{_path} {GetFrameworkOption()} {GetRuntimeOption()} {GetOutputOption()} {GetConfigOption()}";
        }

        private string GetFrameworkOption()
        {
            return string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";
        }

        private string GetRuntimeOption()
        {
            return string.IsNullOrEmpty(_runtime) ? "" : $"-r {_runtime}";
        }

        private string GetOutputOption()
        {
            return string.IsNullOrEmpty(_output) ? "" : $"-o {_output}";
        }

        private string GetConfigOption()
        {
            return string.IsNullOrEmpty(_config) ? "" : $"-c {_output}";
        }
    }
}
