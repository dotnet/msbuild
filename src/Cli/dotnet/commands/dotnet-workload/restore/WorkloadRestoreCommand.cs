// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
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
    internal class WorkloadRestoreCommand : CommandBase
    {
        private readonly ParseResult _result;
        private readonly IReporter _reporter;
        private readonly IEnumerable<string> _slnOrProjectArgument;

        public WorkloadRestoreCommand(
            ParseResult result,
            IFileSystem fileSystem = null,
            IReporter reporter = null)
            : base(result)
        {
            _result = result;
            _reporter = reporter ?? Reporter.Output;
            _slnOrProjectArgument =
                result.ValueForArgument<IEnumerable<string>>(RestoreCommandParser.SlnOrProjectArgument);
        }

        public override int Execute()
        {
            var allProjects = DiscoverAllProjects(Directory.GetCurrentDirectory(), _slnOrProjectArgument).Distinct();
            List<WorkloadId> allWorkloadId = RunTargetToGetWorkloadIds(allProjects);
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingWorkloads, string.Join(" ", allWorkloadId)));

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
                        new ConsoleLogger(_result
                            .ValueForOption<VerbosityOptions>(WorkloadInstallCommandParser.VerbosityOption)
                            .ToLoggerVerbosity())
                    },
                    remoteLoggers: Enumerable.Empty<ForwardingLoggerRecord>(),
                    targetOutputs: out var targetOutputs);

                if (buildResult == false)
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.FailedToRunTarget,
                            projectFile));
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
                var projects = solutionFile.ProjectsInOrder;
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
