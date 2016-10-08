// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;

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
        private readonly bool _noBuild;
        private readonly string _output;
        private readonly string _buidBasePathDirectory;

        public PublishCommand(string projectPath,
            string framework = "",
            string runtime = "",
            string output = "",
            string config = "",
            bool noBuild = false,
            string buildBasePath = "")
            : base("dotnet")
        {
            _path = projectPath;
            _project = ProjectReader.GetProject(projectPath);
            _framework = framework;
            _runtime = runtime;
            _output = output;
            _config = config;
            _noBuild = noBuild;
            _buidBasePathDirectory = buildBasePath;
        }

        public override Task<CommandResult> ExecuteAsync(string args = "")
        {
            args = $"publish {BuildArgs()} {args}";
            return base.ExecuteAsync(args);
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
                var runtime = string.IsNullOrEmpty(_runtime) ? 
                    DotnetRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier() : 
                    _runtime;
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

        public string GetPortableOutputName()
        {
            return $"{_project.Name}.dll";
        }

        public string GetOutputExecutable()
        {
            return _project.Name + GetExecutableExtension();
        }

        public string GetExecutableExtension()
        {
#if NET451
            return ".exe";
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
#endif
        }

        private string BuildArgs()
        {
            return $"{_path} {FrameworkOption} {RuntimeOption} {OutputOption} {ConfigOption} {NoBuildFlag} {BuildBasePathOption}";
        }

        private string FrameworkOption => string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";
        private string RuntimeOption => string.IsNullOrEmpty(_runtime) ? "" : $"-r {_runtime}";
        private string OutputOption => string.IsNullOrEmpty(_output) ? "" : $"-o \"{_output}\"";
        private string ConfigOption => string.IsNullOrEmpty(_config) ? "" : $"-c {_output}";
        private string NoBuildFlag => _noBuild ? "--no-build" : "";
        private string BuildBasePathOption => string.IsNullOrEmpty(_buidBasePathDirectory) ? "" : $"-b {_buidBasePathDirectory}";
    }
}
