// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Tools.New
{
    internal class NewCommandShim
    {
        public const string CommandName = "new";
        private const string HostIdentifier = "dotnetcli";

        public static int Run(string[] args)
        {
            var sessionId =
                Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);
            var telemetry = new Telemetry(new FirstTimeUseNoticeSentinel(), sessionId);
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

            return New3Command.Run(CommandName, CreateHost(), logger, FirstRun, args);
        }

        private static ITemplateEngineHost CreateHost()
        {
            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,
                typeof(ConditionalConfig).GetTypeInfo().Assembly,
            });

            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" },
                { "dotnet-cli-version", Product.Version },
                { "RuntimeFrameworkVersion", new Muxer().SharedFxVersion },
                { "NetStandardImplicitPackageVersion", new FrameworkDependencyFile().GetNetStandardLibraryVersion() },
            };

            return new DefaultTemplateEngineHost(HostIdentifier, "v" + Product.Version, CultureInfo.CurrentCulture.Name, preferences, builtIns);
        }

        private static void FirstRun(IEngineEnvironmentSettings environmentSettings, IInstaller installer)
        {
            Paths paths = new Paths(environmentSettings);
            var templatesDir = Path.Combine(paths.Global.BaseDir, "Templates");

            if (paths.Exists(templatesDir))
            {
                var layoutIncludedPackages = environmentSettings.Host.FileSystem.EnumerateFiles(templatesDir, "*.nupkg", SearchOption.TopDirectoryOnly);
                installer.InstallPackages(layoutIncludedPackages);
            }
        }
    }
}
