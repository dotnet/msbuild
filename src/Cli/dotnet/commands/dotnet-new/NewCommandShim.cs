// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.DotNet.Tools.New
{
    internal static class NewCommandShim
    {
        public const string CommandName = "new";
        private const string HostIdentifier = "dotnetcli";

        public static int Run(string[] args)
        {
            var sessionId =
                Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

            // senderCount: 0 to disable sender.
            // When senders in different process running at the same
            // time they will read from the same global queue and cause
            // sending duplicated events. Disable sender to reduce it.
            var telemetry = new Telemetry(new FirstTimeUseNoticeSentinel(),
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

            New3Callbacks callbacks = new New3Callbacks()
            {
                RestoreProject = RestoreProject
            };

            var disableSdkTemplates = args.Contains("--debug:disable-sdk-templates");

            return New3Command.Run(CommandName, CreateHost(disableSdkTemplates), logger, callbacks, args, null);
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
            return RestoreCommand.Run(new string[] { pathToRestore }) == 0;
        }
    }
}
