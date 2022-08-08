// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

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
using Microsoft.TemplateEngine.MSBuildEvaluation;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using System.IO;
using NuGet.Packaging;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.DotNet.Tools.New.PostActionProcessors;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-new";
        public const string CommandName = "new";
        private const string EnableProjectContextEvaluationEnvVar = "DOTNET_CLI_DISABLE_PROJECT_EVAL";

        private const string HostIdentifier = "dotnetcli";
        private static readonly Option<bool> _disableSdkTemplates = new Option<bool>("--debug:disable-sdk-templates", () => false, LocalizableStrings.DisableSdkTemplates_OptionDescription).Hide();

        private static readonly Option<bool> _disableProjectContextEvaluation = new Option<bool>("--debug:disable-project-context", () => false, LocalizableStrings.DisableProjectContextEval_OptionDescription).Hide();

        internal static Option<FileInfo> ProjectPathOption { get; } = new Option<FileInfo>("--project", LocalizableStrings.ProjectPath_OptionDescription).ExistingOnly().Hide();

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

            var getEngineHost = (ParseResult parseResult) => {
                bool disableSdkTemplates = parseResult.GetValueForOption(_disableSdkTemplates);
                bool disableProjectContext = parseResult.GetValueForOption(_disableProjectContextEvaluation)
                    || Env.GetEnvironmentVariableAsBool(EnableProjectContextEvaluationEnvVar);
                FileInfo? projectPath = parseResult.GetValueForOption(ProjectPathOption);

                //TODO: read and pass output directory
                return CreateHost(disableSdkTemplates, disableProjectContext, projectPath);
            };

            var command = Microsoft.TemplateEngine.Cli.NewCommandFactory.Create(CommandName, getEngineHost, getLogger);

            // adding this option lets us look for its bound value during binding in a typed way
            command.AddGlobalOption(_disableSdkTemplates);
            command.AddGlobalOption(_disableProjectContextEvaluation);
            command.AddGlobalOption(ProjectPathOption);
            return command;
        }

        private static ITemplateEngineHost CreateHost(bool disableSdkTemplates, bool disableProjectContext, FileInfo? projectPath)
        {
            var builtIns = new List<(Type InterfaceType, IIdentifiedComponent Instance)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Cli.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateSearch.Common.Components.AllComponents);

            //post actions
            builtIns.AddRange(new (Type, IIdentifiedComponent)[]
            {
                (typeof(IPostActionProcessor), new DotnetAddPostActionProcessor()),
                (typeof(IPostActionProcessor), new DotnetSlnPostActionProcessor()),
                (typeof(IPostActionProcessor), new DotnetRestorePostActionProcessor()),
            });
            if (!disableSdkTemplates)
            {
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackageProviderFactory()));
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new OptionalWorkloadProviderFactory()));
            }
            if (!disableProjectContext)
            {
                builtIns.Add((typeof(IBindSymbolSource), new ProjectContextSymbolSource()));
                builtIns.Add((typeof(ITemplateConstraintFactory), new ProjectCapabilityConstraintFactory()));
                builtIns.Add((typeof(MSBuildEvaluator), new MSBuildEvaluator(outputDirectory: null, projectPath: projectPath?.FullName)));
            }
            builtIns.Add((typeof(IWorkloadsInfoProvider), new WorkloadsInfoProvider(new WorkloadInfoHelper())));
            builtIns.Add((typeof(ISdkInfoProvider), new SdkInfoProvider()));

            string? preferredLangEnvVar = Environment.GetEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG");
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


    }
}
