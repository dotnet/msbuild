// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class Run3Command
    {
        private const string GetRunInformationTaskName = "GetRunInformation";

        public string Configuration { get; set; }
        public string Project { get; set; }
        public IReadOnlyList<string> Args { get; set; }

        private readonly ICommandFactory _commandFactory;
        private List<string> _args;

        public Run3Command()
            : this(new RunCommandFactory())
        {
        }

        public Run3Command(ICommandFactory commandFactory)
        {
            _commandFactory = commandFactory;
        }

        public int Start()
        {
            Initialize();

            EnsureProjectIsBuilt();

            ITaskItem runInfoItem = GetRunInformation();

            string commandName = runInfoItem.GetMetadata("CommandName");
            string[] args = runInfoItem.GetMetadata("Args").Split(';');

            ICommand command = _commandFactory.Create(commandName, Enumerable.Concat(args, _args));

            return command
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

        private ITaskItem GetRunInformation()
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

            BuildRequestData buildRequestData = new BuildRequestData(projectInstance, new string[] { GetRunInformationTaskName });
            BuildParameters buildParameters = new BuildParameters();

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            TargetResult runInfoResult;
            if (!result.ResultsByTarget.TryGetValue(GetRunInformationTaskName, out runInfoResult))
            {
                throw new InvalidOperationException($"Could not find a target named '{GetRunInformationTaskName}' in your project. Please ensure 'dotnet run' supports this project.");
            }

            if (runInfoResult.ResultCode != TargetResultCode.Success)
            {
                throw new InvalidOperationException($"Could not get the run information for project {Project}. An internal MSBuild error has occured" + Environment.NewLine +
                    runInfoResult.Exception?.ToString());
            }

            ITaskItem runInfoItem = runInfoResult.Items.FirstOrDefault(i => i.ItemSpec == projectInstance.FullPath);
            if (runInfoItem == null)
            {
                throw new InvalidOperationException($"'{GetRunInformationTaskName}' did not return an ITaskItem with the project's FullPath as the ItemSpec.");
            }

            return runInfoItem;
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

        private class RunCommandFactory : ICommandFactory
        {
            public ICommand Create(string commandName, IEnumerable<string> args, NuGetFramework framework = null, string configuration = Constants.DefaultConfiguration)
            {
                return Command.Create(commandName, args, framework, configuration);
            }
        }
    }
}
