using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common.TemplateUpdate;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateInstallTests
{
    public class NupkgInstallTests
    {
        private static readonly string HostIdentifier = "installTestHost";
        private static readonly string HostVersion = "1.0.0";
        private static readonly string CommandName = "new3";

        [Fact(DisplayName = nameof(NupkgReinstallDoesntRemoveTemplates))]
        public void NupkgReinstallDoesntRemoveTemplates()
        {
            const string nupkgToInstallName = "TestNupkgInstallTemplate.0.0.1.nupkg";
            const string checkTemplateName = "nupkginstall";    // this is the short name of the template in the nupkg that gets installed.

            ITemplateEngineHost host = CreateHostWithVirtualizedHive(HostIdentifier, HostVersion);
            Assert.NotNull(host);

            ITelemetryLogger telemetryLogger = new TelemetryLogger(null, false);
            int initializeResult = New3Command.Run(CommandName, host, telemetryLogger, null, new string[] { });
            Assert.Equal(0, initializeResult);

            string codebase = typeof(NupkgInstallTests).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);
            string pathToInstall = Path.Combine(dir, "TemplateInstallTests", "TestTemplates", nupkgToInstallName);

            Assert.True(File.Exists(pathToInstall), $"directory didnt exist: {pathToInstall}");

            string[] installArgs = new[]
            {
                "--install",
                pathToInstall
            };

            // install the test pack
            int firstInstallResult = New3Command.Run(CommandName, host, telemetryLogger, null, installArgs);
            Assert.Equal(0, firstInstallResult);

            EngineEnvironmentSettings environemnt = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            SettingsLoader settingsLoader = (SettingsLoader)environemnt.SettingsLoader;
            IHostSpecificDataLoader hostDataLoader = new MockHostSpecificDataLoader();

            // check that the template was installed from the first install.
            IReadOnlyCollection<ITemplateMatchInfo> allTemplates = TemplateResolver.PerformAllTemplatesQuery(settingsLoader.UserTemplateCache.TemplateInfo, hostDataLoader);
            Assert.Contains(checkTemplateName, allTemplates.Select(t => t.Info.ShortName));

            // install the same test pack again
            int secondInstallResult = New3Command.Run(CommandName, host, telemetryLogger, null, installArgs);
            Assert.Equal(0, secondInstallResult);

            settingsLoader.Reload();

            // check that the template is still installed after the second install.
            IReadOnlyCollection<ITemplateMatchInfo> allTemplatesAfterSecondInstall = TemplateResolver.PerformAllTemplatesQuery(settingsLoader.UserTemplateCache.TemplateInfo, hostDataLoader);
            Assert.Contains(checkTemplateName, allTemplatesAfterSecondInstall.Select(t => t.Info.ShortName));
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

            string hivePath = Path.Combine(profileDir, ".templateengine", hostIdentifier);
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
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,            // for assembly: Microsoft.TemplateEngine.Orchestrator.RunnableProjects
                typeof(NupkgInstallUnitDescriptorFactory).GetTypeInfo().Assembly,   // for assembly: Microsoft.TemplateEngine.Edge
                typeof(DotnetRestorePostActionProcessor).GetTypeInfo().Assembly,    // for assembly: Microsoft.TemplateEngine.Cli
                typeof(NupkgUpdater).GetTypeInfo().Assembly                         // for assembly: Microsoft.TemplateSearch.Common
            });

            return new DefaultTemplateEngineHost(hostIdentifier, hostVersion, preferences, builtIns, new[] { "dotnetcli" });
        }
    }
}
