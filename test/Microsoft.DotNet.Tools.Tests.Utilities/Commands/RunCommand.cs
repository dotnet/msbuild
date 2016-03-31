// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        private string ProjectPathOption
        {
            get
            {
                return _projectPath == string.Empty ?
                                       "" :
                                       $"-p \"{_projectPath}\"";
            }
        }

        private string FrameworkOption
        {
            get
            {
                return _framework == string.Empty ?
                                       "" :
                                       $"-f {_framework}";
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

        private string PreserveTemporaryOption
        {
            get
            {
                return _preserveTemporary ?
                                       $"-t \"{_projectPath}\"" :
                                       "";
            }
        }

        private string AppArgsArgument
        {
            get { return _appArgs; }
        }

        public RunCommand(
            string projectPath,
            string framework="",
            string configuration="",
            bool preserveTemporary=false,
            string appArgs="")
            : base("dotnet")
        {
            _projectPath = projectPath;
            _framework = framework;
            _configuration = configuration;
            _preserveTemporary = preserveTemporary;
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

        private string BuildArgs()
        {
            return $"{ProjectPathOption} {FrameworkOption} {ConfigurationOption} {PreserveTemporaryOption} {AppArgsArgument}";
        }
    }
}
