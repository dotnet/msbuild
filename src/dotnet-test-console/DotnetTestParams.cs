// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using static System.Int32;

namespace Microsoft.DotNet.Tools.Test
{
    public class DotnetTestParams
    {
        private readonly CommandLineApplication _app;

        private CommandOption _outputOption;
        private CommandOption _buildBasePath;
        private CommandOption _frameworkOption;
        private CommandOption _runtimeOption;
        private CommandOption _configurationOption;
        private CommandOption _portOption;
        private CommandOption _parentProcessIdOption;
        private CommandArgument _projectOrAssemblyPath;
        private CommandOption _noBuildOption;
        private CommandOption _testRunner;

        public int? Port { get; set; }

        public int? ParentProcessId { get; set; }

        public string Runtime { get; set; }

        public string Config { get; set; }

        public string BuildBasePath { get; set; }

        public string Output { get; set; }

        public string ProjectOrAssemblyPath { get; set; }

        public NuGetFramework Framework { get; set; }

        public string UnparsedFramework { get; set; }

        public List<string> RemainingArguments { get; set; }

        public bool NoBuild { get; set; }

        public bool Help { get; set; }

        public string TestRunner { get; set; }

        public bool HasTestRunner => !string.IsNullOrWhiteSpace(TestRunner);

        public DotnetTestParams()
        {
            _app = new CommandLineApplication(false)
            {
                Name = "dotnet test",
                FullName = ".NET Test Driver",
                Description = "Test Driver for the .NET Platform"
            };

            AddDotnetTestParameters();

            Help = true;
        }

        public void Parse(string[] args)
        {
            _app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                ProjectOrAssemblyPath = _projectOrAssemblyPath.Value;
                if (string.IsNullOrEmpty(ProjectOrAssemblyPath))
                {
                    ProjectOrAssemblyPath = Directory.GetCurrentDirectory();
                }

                if (_parentProcessIdOption.HasValue())
                {
                    int processId;

                    if (!TryParse(_parentProcessIdOption.Value(), out processId))
                    {
                        throw new InvalidOperationException(
                            $"Invalid process id '{_parentProcessIdOption.Value()}'. Process id must be an integer.");
                    }

                    ParentProcessId = processId;
                }

                if (_portOption.HasValue())
                {
                    int port;

                    if (!TryParse(_portOption.Value(), out port))
                    {
                        throw new InvalidOperationException($"{_portOption.Value()} is not a valid port number.");
                    }

                    Port = port;
                }

                if (_testRunner.HasValue())
                {
                    if (!IsAssembly(ProjectOrAssemblyPath))
                    {
                        throw new InvalidOperationException("You can only specify a test runner with a dll.");
                    }

                    TestRunner = _testRunner.Value();
                }

                UnparsedFramework = _frameworkOption.Value();
                if (_frameworkOption.HasValue())
                {
                    Framework = NuGetFramework.Parse(_frameworkOption.Value());
                }

                Output = _outputOption.Value();
                BuildBasePath = PathUtility.GetFullPath(_buildBasePath.Value());
                Config = _configurationOption.Value() ?? Constants.DefaultConfiguration;
                Runtime = _runtimeOption.Value();
                NoBuild = _noBuildOption.HasValue();

                RemainingArguments = _app.RemainingArguments;

                Help = false;

                return 0;
            });

            _app.Execute(args);
        }

        private bool IsAssembly(string projectOrAssemblyPath)
        {
            return projectOrAssemblyPath.EndsWith(".dll");
        }

        private void AddDotnetTestParameters()
        {
            _app.HelpOption("-?|-h|--help");

            _parentProcessIdOption = _app.Option(
                "--parentProcessId",
                "Used by IDEs to specify their process ID. Test will exit if the parent process does.",
                CommandOptionType.SingleValue);
            _portOption = _app.Option(
                "--port",
                "Used by IDEs to specify a port number to listen for a connection.",
                CommandOptionType.SingleValue);
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
            _noBuildOption =
                _app.Option("--no-build", "Do not build project before testing", CommandOptionType.NoValue);
            _testRunner =
                _app.Option(
                    "-t|--test-runner <TEST_RUNNER>",
                    "Test runner to be used to run the test when an assembly to test is specified. If this option " +
                    "is not provided, we will try to find a suitable runner collocated with the assembly",
                    CommandOptionType.SingleValue);
            _projectOrAssemblyPath = _app.Argument(
                "<PROJECT OR ASSEMBLY TO TEST>",
                "The project or assembly to test, defaults to the current directory. Can be a path to a " +
                "project.json, to a dll or a project directory.");
        }
    }
}
