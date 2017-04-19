// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Configuration { get; private set; }
        public string Framework { get; private set; }
        public bool NoBuild { get; private set; }
        public string Project { get; private set; }
        public IReadOnlyCollection<string> Args { get; private set; }

        private List<string> _args;
        private bool ShouldBuild => !NoBuild;

        public int Start()
        {
            Initialize();

            if (ShouldBuild)
            {
                EnsureProjectIsBuilt();
            }

            ICommand runCommand = GetRunCommand();

            return runCommand
                .Execute()
                .ExitCode;
        }

        public RunCommand(string configuration,
            string framework,
            bool noBuild,
            string project,
            IReadOnlyCollection<string> args)
        {
            Configuration = configuration;
            Framework = framework;
            NoBuild = noBuild;
            Project = project;
            Args = args;
        }

        public RunCommand MakeNewWithReplaced(string configuration = null,
            string framework = null,
            bool? noBuild = null,
            string project = null,
            IReadOnlyCollection<string> args = null)
        {
            return new RunCommand(
                configuration ?? this.Configuration,
                framework ?? this.Framework,
                noBuild ?? this.NoBuild,
                project ?? this.Project,
                args ?? this.Args
            );
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
            var globalProperties = new Dictionary<string, string>
            {
                { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
            };

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                globalProperties.Add("Configuration", Configuration);
            }

            if (!string.IsNullOrWhiteSpace(Framework))
            {
                globalProperties.Add("TargetFramework", Framework);
            }

            Project project = new Project(Project, globalProperties, null);

            string runProgram = project.GetPropertyValue("RunCommand");
            if (string.IsNullOrEmpty(runProgram))
            {
                ThrowUnableToRunError(project);
            }

            string runArguments = project.GetPropertyValue("RunArguments");
            string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

            string fullArguments = runArguments;
            if (_args.Any())
            {
                fullArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(_args);
            }

            CommandSpec commandSpec = new CommandSpec(runProgram, fullArguments, CommandResolutionStrategy.None);

            return Command.Create(commandSpec)
                .WorkingDirectory(runWorkingDirectory);
        }

        private void ThrowUnableToRunError(Project project)
        {
            string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                string targetFramework = project.GetPropertyValue("TargetFramework");
                if (string.IsNullOrEmpty(targetFramework))
                {
                    var framework = "--framework";

                    throw new GracefulException(LocalizableStrings.RunCommandExceptionUnableToRunSpecifyFramework, framework);
                }
            }

            string outputType = project.GetPropertyValue("OutputType");

            throw new GracefulException(
                    string.Format(
                        LocalizableStrings.RunCommandExceptionUnableToRun,
                        "dotnet run",
                        "OutputType",
                        outputType));
        }

        private void Initialize()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                Project = Directory.GetCurrentDirectory();
            } 
            
            if (Directory.Exists(Project)) 
            {
                Project = FindSingleProjectInDirectory(Project);
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

        private static string FindSingleProjectInDirectory(string directory)
        {
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                var project = "--project";

                throw new GracefulException(LocalizableStrings.RunCommandExceptionNoProjects, directory, project);
            }
            else if (projectFiles.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }
    }
}
