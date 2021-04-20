// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateInstallTests
{
    public class NupkgInstallTests
    {
        private static readonly string HostIdentifier = "installTestHost";
        private static readonly string HostVersion = "v1.0.0";
        private static readonly string CommandName = "new3";

        [Fact(DisplayName = nameof(NupkgReinstallDoesntRemoveTemplates))]
        public async Task NupkgReinstallDoesntRemoveTemplates()
        {
            const string nupkgToInstallName = "TestNupkgInstallTemplate.0.0.1.nupkg";
            const string checkTemplateName = "nupkginstall";    // this is the short name of the template in the nupkg that gets installed.

            ITemplateEngineHost host = CreateHostWithVirtualizedHive(HostIdentifier, HostVersion);
            Assert.NotNull(host);

            ITelemetryLogger telemetryLogger = new TelemetryLogger(null, false);
            int initializeResult = New3Command.Run(CommandName, host, telemetryLogger, new New3Callbacks(), Array.Empty<string>());
            Assert.Equal(0, initializeResult);

            string codebase = typeof(NupkgInstallTests).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);

            string pathToInstall = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "nupkg_templates", nupkgToInstallName);

            Assert.True(File.Exists(pathToInstall), $"directory didnt exist: {pathToInstall}");

            string[] installArgs = new[]
            {
                "--install",
                pathToInstall
            };

            // install the test pack
            int firstInstallResult = New3Command.Run(CommandName, host, telemetryLogger, new New3Callbacks(), installArgs);
            Assert.Equal(0, firstInstallResult);

            EngineEnvironmentSettings environemnt = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            SettingsLoader settingsLoader = (SettingsLoader)environemnt.SettingsLoader;
            IHostSpecificDataLoader hostDataLoader = new MockHostSpecificDataLoader();

            // check that the template was installed from the first install.
            IReadOnlyCollection<ITemplateMatchInfo> allTemplates = TemplateResolver.PerformAllTemplatesQuery(await settingsLoader.GetTemplatesAsync(default).ConfigureAwait(false), hostDataLoader);
            Assert.Contains(checkTemplateName, allTemplates.SelectMany(t => t.Info.ShortNameList));

            // install the same test pack again
            int secondInstallResult = New3Command.Run(CommandName, host, telemetryLogger, new New3Callbacks(), installArgs);
            Assert.NotEqual(0, secondInstallResult);

            // check that the template is still installed after the second install.
            IReadOnlyCollection<ITemplateMatchInfo> allTemplatesAfterSecondInstall = TemplateResolver.PerformAllTemplatesQuery(await settingsLoader.GetTemplatesAsync(default).ConfigureAwait(false), hostDataLoader);
            Assert.Contains(checkTemplateName, allTemplatesAfterSecondInstall.SelectMany(t => t.Info.ShortNameList));
        }

        private static ITemplateEngineHost CreateHostWithVirtualizedHive(string hostIdentifier, string hostVersion)
        {
            ITemplateEngineHost host = CreateHost(hostIdentifier, hostVersion);

            string home = "%USERPROFILE%";

            if (Path.DirectorySeparatorChar == '/')
            {
                home = "%HOME%";
            }

            string profileDir = Environment.ExpandEnvironmentVariables(home);

            if (string.IsNullOrWhiteSpace(profileDir))
            {
                // Could not determine home directory
                return null;
            }

            string hivePath = Path.Combine(profileDir, ".templateengine");
            host.VirtualizeDirectory(hivePath);

            return host;
        }

        private static ITemplateEngineHost CreateHost(string hostIdentifier, string hostVersion)
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string versionString = Dotnet.Version().CaptureStdOut().Execute().StdOut;
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IMacro).GetTypeInfo().Assembly,            // for assembly: Microsoft.TemplateEngine.Orchestrator.RunnableProjects
                typeof(Microsoft.TemplateEngine.Edge.Paths).GetTypeInfo().Assembly,   // for assembly: Microsoft.TemplateEngine.Edge
                typeof(DotnetRestorePostActionProcessor).GetTypeInfo().Assembly,    // for assembly: Microsoft.TemplateEngine.Cli
                typeof(Microsoft.TemplateSearch.Common.NuGetSearchCacheConfig).GetTypeInfo().Assembly// for assembly: Microsoft.TemplateSearch.Common
            });

            return new DefaultTemplateEngineHost(hostIdentifier, hostVersion, preferences, builtIns, new[] { "dotnetcli" });
        }
    }
}
