// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class RunCommand : TestCommand
    {
        private string _projectPath;
        private string _framework;
        private string _configuration;
        private bool _preserveTemporary;
        private string _appArgs;

        private string ProjectPathOption => string.IsNullOrEmpty(_projectPath) ? "" : $"-p \"{_projectPath}\"";

        private string FrameworkOption => string.IsNullOrEmpty(_framework) ? "" : $"-f {_framework}";

        private string ConfigurationOption => string.IsNullOrEmpty(_configuration) ? "" : $"-c {_configuration}";

        private string AppArgsArgument => _appArgs;

        public RunCommand(
            string projectPath,
            string framework = "",
            string configuration = "",
            string appArgs = "")
            : base("dotnet")
        {
            _projectPath = projectPath;
            _framework = framework;
            _configuration = configuration;
            _appArgs = appArgs;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"run {BuildArgs()} {args}";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"run {BuildArgs()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public override Task<CommandResult> ExecuteAsync(string args = "")
        {
            args = $"run {BuildArgs()} {args}";
            return base.ExecuteAsync(args);
        }

        private string BuildArgs()
        {
            return string.Join(" ",
                new[]
                {
                    ProjectPathOption,
                    FrameworkOption,
                    ConfigurationOption,
                    AppArgsArgument,
                }
                .Where(s => !string.IsNullOrEmpty(s)));
        }
    }
}
