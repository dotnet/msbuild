// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.DependencyInvoker
{
    public class DotnetBaseParams
    {
        private readonly CommandLineApplication _app;

        private CommandOption _outputOption;
        private CommandOption _buildBasePath;
        private CommandOption _frameworkOption;
        private CommandOption _runtimeOption;
        private CommandOption _configurationOption;
        private CommandOption _projectPath;
        private CommandArgument _command;

        public string Runtime { get; set; }

        public string Config { get; set; }

        public string BuildBasePath { get; set; }

        public string Output { get; set; }

        public string ProjectPath { get; set; }

        public NuGetFramework Framework { get; set; }

        public string Command {get; set; }

        public List<string> RemainingArguments { get; set; }

        public DotnetBaseParams(string name, string fullName, string description)
        {
            _app = new CommandLineApplication(false)
            {
                Name = name,
                FullName = fullName,
                Description = description
            };

            AddDotnetBaseParameters();
        }

        public void Parse(string[] args)
        {
            _app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                ProjectPath = _projectPath.Value();
                Output = _outputOption.Value();
                BuildBasePath = _buildBasePath.Value();
                Config = _configurationOption.Value() ?? Constants.DefaultConfiguration;
                Runtime = _runtimeOption.Value();
                if (_frameworkOption.HasValue())
                {
                    Framework = NuGetFramework.Parse(_frameworkOption.Value());
                }
                Command = _command.Value;
                RemainingArguments = _app.RemainingArguments;
                
                if (string.IsNullOrEmpty(ProjectPath))
                {
                    ProjectPath = Directory.GetCurrentDirectory();
                }

                return 0;
            });

            _app.Execute(args);
        }

        private void AddDotnetBaseParameters()
        {
            _app.HelpOption("-?|-h|--help");
            
            _configurationOption = _app.Option(
                "-c|--configuration <CONFIGURATION>",
                "Configuration under which to build",
                CommandOptionType.SingleValue);
            _outputOption = _app.Option(
                "-o|--output <OUTPUT_DIR>",
                "Directory in which to find the binaries to be run",
                CommandOptionType.SingleValue);
            _buildBasePath = _app.Option(
                "-b|--build-base-path <OUTPUT_DIR>",
                "Directory in which to find temporary outputs",
                CommandOptionType.SingleValue);
            _frameworkOption = _app.Option(
                "-f|--framework <FRAMEWORK>",
                "Looks for test binaries for a specific framework",
                CommandOptionType.SingleValue);
            _runtimeOption = _app.Option(
                "-r|--runtime <RUNTIME_IDENTIFIER>",
                "Look for test binaries for a for the specified runtime",
                CommandOptionType.SingleValue);  
            _projectPath = _app.Option(
                "-p|--project-path <PROJECT_JSON_PATH>",
                "Path to Project.json that contains the tool dependency",
                CommandOptionType.SingleValue);
            _command = _app.Argument(
                "<COMMAND>",
                "The command to execute.");
        }
    }
}
