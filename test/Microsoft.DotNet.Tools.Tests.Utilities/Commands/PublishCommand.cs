// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.PlatformAbstractions;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class PublishCommand : TestCommand
    {
        private const string PublishSubfolderName = "publish";

        private readonly Project _project;
        private readonly string _path;
        private readonly string _framework;
        private readonly string _runtime;
        private readonly string _config;
        private readonly string _output;

        public PublishCommand(string projectPath, string framework = "", string runtime = "", string output = "", string config = "", bool forcePortable = false)
            : base("dotnet")
        {
            _path = projectPath;
            _project = ProjectReader.GetProject(projectPath);
            _framework = framework;
            _runtime = runtime;
            _output = output;
            _config = config;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public string ProjectName => _project.Name;

        private string BuildRelativeOutputPath(bool portable)
        {
            // lets try to build an approximate output path
            string config = string.IsNullOrEmpty(_config) ? "Debug" : _config;
            string framework = string.IsNullOrEmpty(_framework) ?
                _project.GetTargetFrameworks().First().FrameworkName.GetShortFolderName() : _framework;
            if (!portable)
            {
                var runtime = string.IsNullOrEmpty(_runtime) ? PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier() : _runtime;
                return Path.Combine(config, framework, runtime, PublishSubfolderName);
            }
            else
            {
                return Path.Combine(config, framework, PublishSubfolderName);
            }
        }

        public DirectoryInfo GetOutputDirectory(bool portable = false)
        {
            if (!string.IsNullOrEmpty(_output))
            {
                return new DirectoryInfo(_output);
            }

            string output = Path.Combine(_project.ProjectDirectory, "bin", BuildRelativeOutputPath(portable));
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
            return $"{_path} {FrameworkOption} {RuntimeOption} {OutputOption} {ConfigOption}";
        }

        private string FrameworkOption => string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";
        private string RuntimeOption => string.IsNullOrEmpty(_runtime) ? "" : $"-r {_runtime}";
        private string OutputOption => string.IsNullOrEmpty(_output) ? "" : $"-o \"{_output}\"";
        private string ConfigOption => string.IsNullOrEmpty(_config) ? "" : $"-c {_output}";
    }
}
