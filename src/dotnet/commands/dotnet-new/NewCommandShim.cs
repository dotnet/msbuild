// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common.TemplateUpdate;

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

            return New3Command.Run(CommandName, CreateHost(), logger, FirstRun, args);
        }

        private static ITemplateEngineHost CreateHost()
        {
            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,
                typeof(ConditionalConfig).GetTypeInfo().Assembly,
                typeof(NupkgUpdater).GetTypeInfo().Assembly
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

            return new DefaultTemplateEngineHost(HostIdentifier, "v" + Product.Version, CultureInfo.CurrentCulture.Name, preferences, builtIns);
        }

        private static void FirstRun(IEngineEnvironmentSettings environmentSettings, IInstaller installer)
        {
            var templateFolders = GetTemplateFolders(environmentSettings);
            foreach (var templateFolder in templateFolders)
            {
                var layoutIncludedPackages = environmentSettings.Host.FileSystem.EnumerateFiles(templateFolder, "*.nupkg", SearchOption.TopDirectoryOnly);
                installer.InstallPackages(layoutIncludedPackages);
            }
        }

        private static IEnumerable<string> GetTemplateFolders(IEngineEnvironmentSettings environmentSettings)
        {
            Paths paths = new Paths(environmentSettings);
            List<string> templateFoldersToInstall = new List<string>();

            // First grab templates from dotnet\templates\M.m folders, in ascending order, up to our version
            var templatesRootFolder = Path.GetFullPath(Path.Combine(paths.Global.BaseDir, "..", "..", "templates"));
            if (paths.Exists(templatesRootFolder))
            {
                var parsedNames = GetVersionDirectoriesInDirectory(environmentSettings, templatesRootFolder);
                var versionedFolders = GetBestVersionsByMajorMinor(parsedNames);

                templateFoldersToInstall.AddRange(versionedFolders
                    .Select(versionedFolder => Path.Combine(templatesRootFolder, versionedFolder)));
            }

            // Now grab templates from our base folder, if present.
            var templatesDir = Path.Combine(paths.Global.BaseDir, "Templates");
            if (paths.Exists(templatesDir))
            {
                templateFoldersToInstall.Add(templatesDir);
            }

            return templateFoldersToInstall;
        }

        // Returns a dictionary of fileName -> Parsed version info
        // including all the directories in the input directory whose names are parse-able as versions.
        private static IReadOnlyDictionary<string, SemanticVersion> GetVersionDirectoriesInDirectory(IEngineEnvironmentSettings environmentSettings, string fullPath)
        {
            Dictionary<string, SemanticVersion> versionFileInfo = new Dictionary<string, SemanticVersion>();

            foreach (string directory in environmentSettings.Host.FileSystem.EnumerateDirectories(fullPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (SemanticVersion.TryParse(Path.GetFileName(directory), out SemanticVersion versionInfo))
                {
                    versionFileInfo.Add(directory, versionInfo);
                }
            }

            return versionFileInfo;
        }

        private static IList<string> GetBestVersionsByMajorMinor(IReadOnlyDictionary<string, SemanticVersion> versionDirInfo)
        {
            IDictionary<string, (string path, SemanticVersion version)> bestVersionsByBucket = new Dictionary<string, (string path, SemanticVersion version)>();

            var sdkVersion = typeof(NewCommandShim).Assembly.GetName().Version;
            foreach (KeyValuePair<string, SemanticVersion> dirInfo in versionDirInfo)
            {
                Version majorMinorDirVersion = new Version(dirInfo.Value.Major, dirInfo.Value.Minor);
                // restrict the results to not include from higher versions of the runtime/templates then the SDK
                if (majorMinorDirVersion <= sdkVersion)
                {
                    string coreAppVersion = $"{dirInfo.Value.Major}.{dirInfo.Value.Minor}";
                    if (!bestVersionsByBucket.TryGetValue(coreAppVersion, out (string path, SemanticVersion version) currentHighest)
                        || dirInfo.Value.CompareTo(currentHighest.version) > 0)
                    {
                        bestVersionsByBucket[coreAppVersion] = (dirInfo.Key, dirInfo.Value);
                    }
                }
            }

            return bestVersionsByBucket.OrderBy(x => x.Key).Select(x => x.Value.path).ToList();
        }
    }
}
