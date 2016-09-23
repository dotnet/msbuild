// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class Run3Command
    {
        public string Configuration { get; set; }
        public string Project { get; set; }
        public IReadOnlyList<string> Args { get; set; }

        private List<string> _args;

        public Run3Command()
        {
        }

        public int Start()
        {
            Initialize();

            EnsureProjectIsBuilt();

            ICommand runCommand = GetRunCommand();

            return runCommand
                .Execute()
                .ExitCode;
        }

        private void EnsureProjectIsBuilt()
        {
            List<string> buildArgs = new List<string>();

            buildArgs.Add("/nologo");
            buildArgs.Add("/verbosity:quiet");

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                buildArgs.Add($"/p:Configuration={Configuration}");
            }

            var buildResult = new MSBuildForwardingApp(buildArgs).Execute();

            if (buildResult != 0)
            {
                Reporter.Error.WriteLine();
                throw new GracefulException("The build failed. Please fix the build errors and run again.");
            }
        }

        private ICommand GetRunCommand()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>()
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory }
            };

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                globalProperties.Add("Configuration", Configuration);
            }

            ProjectInstance projectInstance = new ProjectInstance(Project, globalProperties, null);

            string runProgram = projectInstance.GetPropertyValue("RunCommand");
            if (string.IsNullOrEmpty(runProgram))
            {
                string outputType = projectInstance.GetPropertyValue("OutputType");

                throw new GracefulException(string.Join(Environment.NewLine,
                    "Unable to run your project.",
                    "Please ensure you have a runnable project type and ensure 'dotnet run' supports this project.",
                    $"The current OutputType is '{outputType}'."));
            }

            string runArguments = projectInstance.GetPropertyValue("RunArguments");
            string runWorkingDirectory = projectInstance.GetPropertyValue("RunWorkingDirectory");

            string fullArguments = runArguments;
            if (_args.Any())
            {
                fullArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(_args);
            }

            CommandSpec commandSpec = new CommandSpec(runProgram, fullArguments, CommandResolutionStrategy.None);

            return Command.Create(commandSpec)
                .WorkingDirectory(runWorkingDirectory);
        }

        private void Initialize()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                string directory = Directory.GetCurrentDirectory();
                string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

                if (projectFiles.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Couldn't find a project to run. Ensure a project exists in {directory}." + Environment.NewLine +
                        "Or pass the path to the project using --project");
                }
                else if (projectFiles.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Specify which project file to use because this '{directory}' contains more than one project file.");
                }

                Project = projectFiles[0];
            }

            if (Args == null)
            {
                _args = new List<string>();
            }
            else
            {
                _args = new List<string>(Args);
            }
        }
    }
}
