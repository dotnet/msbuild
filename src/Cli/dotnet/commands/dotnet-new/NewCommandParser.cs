// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.DotNet.Tools.Add.PackageReference;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;
using Microsoft.DotNet.Tools.Sln.Add;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-new";
        public const string CommandName = "new";
        private const string HostIdentifier = "dotnetcli";

        private static readonly Option<bool> _disableSdkTemplates = new Option<bool>("--debug:disable-sdk-templates", () => false, LocalizableStrings.DisableSdkTemplates_OptionDescription).Hide();

        internal static readonly System.CommandLine.Command Command = GetCommand();

        public static System.CommandLine.Command GetCommand()
        {
            var getLogger = (ParseResult parseResult) => {
                var sessionId = Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

                // senderCount: 0 to disable sender.
                // When senders in different process running at the same
                // time they will read from the same global queue and cause
                // sending duplicated events. Disable sender to reduce it.
                var telemetry = new Microsoft.DotNet.Cli.Telemetry.Telemetry(new FirstTimeUseNoticeSentinel(),
                                            sessionId,
                                            senderCount: 0);
                var logger = new TelemetryLogger(null);

                if (telemetry.Enabled)
                {
                    logger = new TelemetryLogger((name, props, measures) =>
                    {
                        if (telemetry.Enabled)
                        {
                            telemetry.TrackEvent($"template/{name}", props, measures);
                        }
                    });
                }
                return logger;
            };

            var callbacks = new Microsoft.TemplateEngine.Cli.NewCommandCallbacks()
            {
                RestoreProject = RestoreProject,
                AddPackageReference = AddPackageReference,
                AddProjectReference = AddProjectReference,
                AddProjectsToSolution = AddProjectsToSolution
            };

            var getEngineHost = (ParseResult parseResult) => {
                var disableSdkTemplates = parseResult.GetValueForOption(_disableSdkTemplates);
                return CreateHost(disableSdkTemplates);
            };

            var command = Microsoft.TemplateEngine.Cli.NewCommandFactory.Create(CommandName, getEngineHost, getLogger, callbacks);

            // adding this option lets us look for its bound value during binding in a typed way
            command.AddGlobalOption(_disableSdkTemplates);
            return command;
        }

        private static ITemplateEngineHost CreateHost(bool disableSdkTemplates)
        {
            var builtIns = new List<(Type InterfaceType, IIdentifiedComponent Instance)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Cli.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateSearch.Common.Components.AllComponents);
            if (!disableSdkTemplates)
            {
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackageProviderFactory()));
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new OptionalWorkloadProviderFactory()));
            }
            builtIns.Add((typeof(IWorkloadsInfoProvider), new WorkloadsInfoProvider(new WorkloadInfoHelper())));
            builtIns.Add((typeof(ISdkInfoProvider), new SdkInfoProvider()));

            string preferredLangEnvVar = Environment.GetEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG");
            string preferredLang = string.IsNullOrWhiteSpace(preferredLangEnvVar)? "C#" : preferredLangEnvVar;

            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", preferredLang },
                { "dotnet-cli-version", Product.Version },
                { "RuntimeFrameworkVersion", new Muxer().SharedFxVersion },
                { "NetStandardImplicitPackageVersion", new FrameworkDependencyFile().GetNetStandardLibraryVersion() },
            };

            return new DefaultTemplateEngineHost(HostIdentifier, "v" + Product.Version, preferences, builtIns);
        }

        private static bool RestoreProject(string pathToRestore)
        {
            try
            {
                PathUtility.EnsureAllPathsExist(new[] { pathToRestore }, CommonLocalizableStrings.FileNotFound, allowDirectories: true);
                return RestoreCommand.Run(new string[] { pathToRestore }) == 0;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RestoreCallback_Failed, e.Message));
                return false;
            }
        }

        private static bool AddPackageReference(string projectPath, string packageName, string version)
        {
            try
            {
                PathUtility.EnsureAllPathsExist(new[] { projectPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
                IEnumerable<string> commandArgs = new [] { "add", projectPath, "package", packageName };
                if (!string.IsNullOrWhiteSpace(version))
                {
                    commandArgs = commandArgs.Append(AddPackageParser.VersionOption.Aliases.First()).Append(version);
                }
                var addPackageReferenceCommand = new AddPackageReferenceCommand(AddCommandParser.GetCommand().Parse(commandArgs.ToArray()));
                return addPackageReferenceCommand.Execute() == 0;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddPackageReferenceCallback_Failed, e.Message));
                return false;
            }
        }

        private static bool AddProjectReference(string projectPath, IReadOnlyList<string> projectsToAdd)
        {
            try
            {
                PathUtility.EnsureAllPathsExist(new[] { projectPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
                PathUtility.EnsureAllPathsExist(projectsToAdd, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
                IEnumerable<string> commandArgs = new[] { "add", projectPath, "reference", }.Concat(projectsToAdd);
                var addProjectReferenceCommand = new AddProjectToProjectReferenceCommand(AddCommandParser.GetCommand().Parse(commandArgs.ToArray()));
                return addProjectReferenceCommand.Execute() == 0;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddProjectReferenceCallback_Failed, e.Message));
                return false;
            }
        }

        private static bool AddProjectsToSolution(string solutionPath, IReadOnlyList<string> projectsToAdd, string solutionFolder)
        {
            try
            {
                PathUtility.EnsureAllPathsExist(new[] { solutionPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
                PathUtility.EnsureAllPathsExist(projectsToAdd, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
                IEnumerable<string> commandArgs = new[] { "sln", solutionPath, "add" }.Concat(projectsToAdd);
                if (!string.IsNullOrWhiteSpace(solutionFolder))
                {
                    commandArgs = commandArgs.Append(SlnAddParser.SolutionFolderOption.Aliases.First()).Append(solutionFolder);
                }
                var addProjectToSolutionCommand = new AddProjectToSolutionCommand(SlnCommandParser.GetCommand().Parse(commandArgs.ToArray()));
                return addProjectToSolutionCommand.Execute() == 0;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddProjectsToSolutionCallback_Failed, e.Message));
                return false;
            }
        }
    }
}
