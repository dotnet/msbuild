// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common;

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

            return New3Command.Run(CommandName, CreateHost(), logger, callbacks, args, null);
        }

        private static ITemplateEngineHost CreateHost()
        {
            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IMacro).Assembly,
                typeof(BuiltInTemplatePackageProviderFactory).Assembly,
                typeof(FileMetadataSearchSource).Assembly
            });

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
