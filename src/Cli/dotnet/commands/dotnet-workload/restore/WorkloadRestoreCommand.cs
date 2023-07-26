// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Restore
{
    internal class WorkloadRestoreCommand : WorkloadCommandBase
    {
        private readonly ParseResult _result;
        private readonly IEnumerable<string> _slnOrProjectArgument;

        public WorkloadRestoreCommand(
            ParseResult result,
            IFileSystem fileSystem = null,
            IReporter reporter = null)
            : base(result, reporter: reporter)
        {
            _result = result;
            _slnOrProjectArgument =
                result.GetValue(RestoreCommandParser.SlnOrProjectArgument);
        }

        public override int Execute()
        {
            var allProjects = DiscoverAllProjects(Directory.GetCurrentDirectory(), _slnOrProjectArgument).Distinct();
            List<WorkloadId> allWorkloadId = RunTargetToGetWorkloadIds(allProjects);
            Reporter.WriteLine(string.Format(LocalizableStrings.InstallingWorkloads, string.Join(" ", allWorkloadId)));

            var workloadInstallCommand = new WorkloadInstallCommand(_result,
                workloadIds: allWorkloadId.Select(a => a.ToString()).ToList().AsReadOnly());

            workloadInstallCommand.Execute();
            return 0;
        }

        private List<WorkloadId> RunTargetToGetWorkloadIds(IEnumerable<string> allProjects)
        {
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"SkipResolvePackageAssets", "true"}
            };

            var allWorkloadId = new List<WorkloadId>();
            foreach (string projectFile in allProjects)
            {
                var project = new ProjectInstance(projectFile, globalProperties, null);

                bool buildResult = project.Build(new[] {"_GetRequiredWorkloads"},
                    loggers: new ILogger[]
                    {
                        new ConsoleLogger(Verbosity.ToLoggerVerbosity())
                    },
                    remoteLoggers: Enumerable.Empty<ForwardingLoggerRecord>(),
                    targetOutputs: out var targetOutputs);

                if (buildResult == false)
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.FailedToRunTarget,
                            projectFile),
                        isUserError: false);
                }

                var targetResult = targetOutputs["_GetRequiredWorkloads"];
                allWorkloadId.AddRange(targetResult.Items.Select(item => new WorkloadId(item.ItemSpec)));
            }

            allWorkloadId = allWorkloadId.Distinct().ToList();
            return allWorkloadId;
        }


        internal static List<string> DiscoverAllProjects(string currentDirectory,
            IEnumerable<string> slnOrProjectArgument = null)
        {
            var slnFiles = new List<string>();
            var projectFiles = new List<string>();
            if (slnOrProjectArgument == null || !slnOrProjectArgument.Any())
            {
                slnFiles = Directory.GetFiles(currentDirectory, "*.sln").ToList();
                projectFiles.AddRange(Directory.GetFiles(currentDirectory, "*.*proj"));
            }
            else
            {
                slnFiles = slnOrProjectArgument
                    .Where(s => Path.GetExtension(s).Equals(".sln", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFullPath).ToList();
                projectFiles = slnOrProjectArgument
                    .Where(s => Path.GetExtension(s).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFullPath).ToList();
            }

            foreach (string file in slnFiles)
            {
                var solutionFile = SolutionFile.Parse(file);
                var projects = solutionFile.ProjectsInOrder.Where(p => p.ProjectType != SolutionProjectType.SolutionFolder);
                foreach (var p in projects)
                {
                    projectFiles.Add(p.AbsolutePath);
                }
            }

            if (projectFiles.Count == 0)
            {
                throw new GracefulException(
                    LocalizableStrings.CouldNotFindAProject,
                    currentDirectory, "--project");
            }

            return projectFiles;
        }
    }
}
