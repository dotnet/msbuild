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
    public partial class RunCommand
    {
        public string Configuration { get; set; }
        public string Framework { get; set; }
        public string Project { get; set; }
        public IReadOnlyList<string> Args { get; set; }

        private List<string> _args;

        public RunCommand()
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
 
            buildArgs.Add(Project); 
 
            buildArgs.Add("/nologo");
            buildArgs.Add("/verbosity:quiet");

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                buildArgs.Add($"/p:Configuration={Configuration}");
            }

            if (!string.IsNullOrWhiteSpace(Framework))
            {
                buildArgs.Add($"/p:TargetFramework={Framework}");
            }

            var buildResult = new MSBuildForwardingApp(buildArgs).Execute();

            if (buildResult != 0)
            {
                Reporter.Error.WriteLine();
                throw new GracefulException(LocalizableStrings.RunCommandException);
            }
        }

        private ICommand GetRunCommand()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>()
            {
                { LocalizableStrings.RunCommandMSBuildExtensionsPath, AppContext.BaseDirectory }
            };

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                globalProperties.Add(LocalizableStrings.RunCommandConfiguration, Configuration);
            }

            if (!string.IsNullOrWhiteSpace(Framework))
            {
                globalProperties.Add(LocalizableStrings.RunCommandTargetFramework, Framework);
            }

            ProjectInstance projectInstance = new ProjectInstance(Project, globalProperties, null);

            string runProgram = projectInstance.GetPropertyValue(LocalizableStrings.RunCommandProjectInstance);
            if (string.IsNullOrEmpty(runProgram))
            {
                string outputType = projectInstance.GetPropertyValue(LocalizableStrings.RunCommandOutputType);

                throw new GracefulException(string.Join(Environment.NewLine,
                    LocalizableStrings.RunCommandExceptionUnableToRun1,
                    LocalizableStrings.RunCommandExceptionUnableToRun2,
                    $"{LocalizableStrings.RunCommandExceptionUnableToRun3} '{outputType}'."));
            }

            string runArguments = projectInstance.GetPropertyValue(LocalizableStrings.RunCommandRunArguments);
            string runWorkingDirectory = projectInstance.GetPropertyValue(LocalizableStrings.RunCommandRunWorkingDirectory);

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
                        $"{LocalizableStrings.RunCommandInvalidOperationException1} {directory}." + Environment.NewLine +
                        LocalizableStrings.RunCommandInvalidOperationException2);
                }
                else if (projectFiles.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"{LocalizableStrings.RunCommandInvalidOperationException3}'{directory}'{LocalizableStrings.RunCommandInvalidOperationException4}");
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
