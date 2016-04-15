using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public static class PublishTargets
    {
        private static AzurePublisher AzurePublisherTool { get; set; }

        private static DebRepoPublisher DebRepoPublisherTool { get; set; }

        private static string Channel { get; set; }

        private static string CliVersion { get; set; }

        private static string CliNuGetVersion { get; set; }

        private static string SharedFrameworkNugetVersion { get; set; }

        [Target]
        public static BuildTargetResult InitPublish(BuildTargetContext c)
        {
            AzurePublisherTool = new AzurePublisher();
            DebRepoPublisherTool = new DebRepoPublisher(Dirs.Packages);

            CliVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            CliNuGetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            SharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            Channel = c.BuildContext.Get<string>("Channel");

            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init),
        nameof(PublishTargets.InitPublish),
        nameof(PublishTargets.PublishArtifacts),
        nameof(PublishTargets.TriggerDockerHubBuilds))]
        [Environment("PUBLISH_TO_AZURE_BLOB", "1", "true")] // This is set by CI systems
        public static BuildTargetResult Publish(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(
            nameof(PublishTargets.PublishInstallerFilesToAzure),
            nameof(PublishTargets.PublishArchivesToAzure),
            nameof(PublishTargets.PublishDebFilesToDebianRepo),
            nameof(PublishTargets.PublishLatestCliVersionTextFile),
            nameof(PublishTargets.PublishLatestSharedFrameworkVersionTextFile),
            nameof(PublishTargets.PublishCoreHostPackages),
            nameof(PublishTargets.PublishCliVersionBadge))]
        public static BuildTargetResult PublishArtifacts(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishTargets.PublishSharedHostInstallerFileToAzure),
            nameof(PublishTargets.PublishSharedFrameworkInstallerFileToAzure),
            nameof(PublishTargets.PublishSdkInstallerFileToAzure),
            nameof(PublishTargets.PublishCombinedFrameworkSDKHostInstallerFileToAzure),
            nameof(PublishTargets.PublishCombinedFrameworkHostInstallerFileToAzure))]
        public static BuildTargetResult PublishInstallerFilesToAzure(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishTargets.PublishCombinedHostFrameworkArchiveToAzure),
            nameof(PublishTargets.PublishCombinedHostFrameworkSdkArchiveToAzure),
            nameof(PublishTargets.PublishSDKSymbolsArchiveToAzure))]
        public static BuildTargetResult PublishArchivesToAzure(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishSdkDebToDebianRepo),
            nameof(PublishSharedFrameworkDebToDebianRepo),
            nameof(PublishSharedHostDebToDebianRepo))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishDebFilesToDebianRepo(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCliVersionBadge(BuildTargetContext c)
        {
            var versionBadge = c.BuildContext.Get<string>("VersionBadge");
            var latestVersionBadgeBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(versionBadge)}";
            var versionBadgeBlob = $"{Channel}/Binaries/{CliNuGetVersion}/{Path.GetFileName(versionBadge)}";

            AzurePublisherTool.PublishFile(versionBadgeBlob, versionBadge);
            AzurePublisherTool.PublishFile(latestVersionBadgeBlob, versionBadge);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCoreHostPackages(BuildTargetContext c)
        {
            foreach (var file in Directory.GetFiles(Dirs.Corehost, "*.nupkg"))
            {
                var hostBlob = $"{Channel}/Binaries/{CliNuGetVersion}/{Path.GetFileName(file)}";
                AzurePublisherTool.PublishFile(hostBlob, file);
                Console.WriteLine($"Publishing package {hostBlob} to Azure.");
            }

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedHostInstallerFileToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var installerFile = c.BuildContext.Get<string>("SharedHostInstallerFile");

            AzurePublisherTool.PublishInstallerFileAndLatest(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedFrameworkInstallerFileToAzure(BuildTargetContext c)
        {
            var version = SharedFrameworkNugetVersion;
            var installerFile = c.BuildContext.Get<string>("SharedFrameworkInstallerFile");

            AzurePublisherTool.PublishInstallerFileAndLatest(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSdkInstallerFileToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");

            AzurePublisherTool.PublishInstallerFileAndLatest(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows, BuildPlatform.OSX)]
        public static BuildTargetResult PublishCombinedFrameworkHostInstallerFileToAzure(BuildTargetContext c)
        {
            var version = SharedFrameworkNugetVersion;
            var installerFile = c.BuildContext.Get<string>("CombinedFrameworkHostInstallerFile");

            AzurePublisherTool.PublishInstallerFileAndLatest(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows, BuildPlatform.OSX)]
        public static BuildTargetResult PublishCombinedFrameworkSDKHostInstallerFileToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var installerFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");

            AzurePublisherTool.PublishInstallerFileAndLatest(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCombinedHostFrameworkSdkArchiveToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile");

            AzurePublisherTool.PublishArchiveAndLatest(archiveFile, Channel, version);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishSDKSymbolsArchiveToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var archiveFile = c.BuildContext.Get<string>("SdkSymbolsCompressedFile");

            AzurePublisherTool.PublishArchiveAndLatest(archiveFile, Channel, version);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCombinedHostFrameworkArchiveToAzure(BuildTargetContext c)
        {
            var version = SharedFrameworkNugetVersion;
            var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkHostCompressedFile");

            AzurePublisherTool.PublishArchiveAndLatest(archiveFile, Channel, version);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishLatestCliVersionTextFile(BuildTargetContext c)
        {
            var version = CliNuGetVersion;

            var osname = Monikers.GetOSShortName();
            var latestCliVersionBlob = $"{Channel}/dnvm/latest.{osname}.{CurrentArchitecture.Current}.version";
            var latestCliVersionFile = Path.Combine(Dirs.Stage2, "sdk", version, ".version");

            AzurePublisherTool.PublishFile(latestCliVersionBlob, latestCliVersionFile);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishLatestSharedFrameworkVersionTextFile(BuildTargetContext c)
        {
            var version = SharedFrameworkNugetVersion;

            var osname = Monikers.GetOSShortName();
            var latestSharedFXVersionBlob = $"{Channel}/dnvm/latest.sharedfx.{osname}.{CurrentArchitecture.Current}.version";
            var latestSharedFXVersionFile = Path.Combine(
                Dirs.Stage2,
                "shared",
                CompileTargets.SharedFrameworkName,
                version,
                ".version");

            AzurePublisherTool.PublishFile(latestSharedFXVersionBlob, latestSharedFXVersionFile);
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSdkDebToDebianRepo(BuildTargetContext c)
        {
            var version = CliNuGetVersion;

            var packageName = Monikers.GetSdkDebianPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var uploadUrl = AzurePublisherTool.CalculateInstallerUploadUrl(installerFile, Channel, version);

            DebRepoPublisherTool.PublishDebFileToDebianRepo(
                packageName,
                version,
                uploadUrl);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedFrameworkDebToDebianRepo(BuildTargetContext c)
        {
            var version = SharedFrameworkNugetVersion;

            var packageName = Monikers.GetDebianSharedFrameworkPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SharedFrameworkInstallerFile");
            var uploadUrl = AzurePublisherTool.CalculateInstallerUploadUrl(installerFile, Channel, version);

            DebRepoPublisherTool.PublishDebFileToDebianRepo(
                packageName,
                version,
                uploadUrl);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedHostDebToDebianRepo(BuildTargetContext c)
        {
            var version = CliNuGetVersion;

            var packageName = Monikers.GetDebianSharedHostPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SharedHostInstallerFile");
            var uploadUrl = AzurePublisherTool.CalculateInstallerUploadUrl(installerFile, Channel, version);

            DebRepoPublisherTool.PublishDebFileToDebianRepo(
                packageName,
                version,
                uploadUrl);

            return c.Success();
        }

        [Target]
        [Environment("DOCKER_HUB_REPO")]
        [Environment("DOCKER_HUB_TRIGGER_TOKEN")]
        public static BuildTargetResult TriggerDockerHubBuilds(BuildTargetContext c)
        {
            string dockerHubRepo = Environment.GetEnvironmentVariable("DOCKER_HUB_REPO");
            string dockerHubTriggerToken = Environment.GetEnvironmentVariable("DOCKER_HUB_TRIGGER_TOKEN");

            Uri baseDockerHubUri = new Uri("https://registry.hub.docker.com/u/");
            Uri dockerHubTriggerUri;
            if (!Uri.TryCreate(baseDockerHubUri, $"{dockerHubRepo}/trigger/{dockerHubTriggerToken}/", out dockerHubTriggerUri))
            {
                return c.Failed("Invalid DOCKER_HUB_REPO and/or DOCKER_HUB_TRIGGER_TOKEN");
            }

            c.Info($"Triggering automated DockerHub builds for {dockerHubRepo}");
            using (HttpClient client = new HttpClient())
            {
                StringContent requestContent = new StringContent("{\"build\": true}", Encoding.UTF8, "application/json");
                try
                {
                    HttpResponseMessage response = client.PostAsync(dockerHubTriggerUri, requestContent).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"HTTP request to {dockerHubTriggerUri.ToString()} was unsuccessful.");
                        sb.AppendLine($"Response status code: {response.StatusCode}. Reason phrase: {response.ReasonPhrase}.");
                        sb.Append($"Respone content: {response.Content.ReadAsStringAsync().Result}");
                        return c.Failed(sb.ToString());
                    }
                }
                catch (AggregateException e)
                {
                    return c.Failed($"HTTP request to {dockerHubTriggerUri.ToString()} failed. {e.ToString()}");
                }
            }
            return c.Success();
        }

        private const string PackagePushedSemaphoreFileName = "packages.pushed";

        [Target(nameof(PrepareTargets.Init), nameof(InitPublish))]
        public static BuildTargetResult PullNupkgFilesFromBlob(BuildTargetContext c)
        {
            Directory.CreateDirectory(Dirs.PackagesNoRID);

            var hostBlob = $"{Channel}/Binaries/";

            string forcePushBuild = Environment.GetEnvironmentVariable("FORCE_PUBLISH_BLOB_BUILD_VERSION");

            if (!string.IsNullOrEmpty(forcePushBuild))
            {
                Console.WriteLine($"Forcing all nupkg packages for build version {forcePushBuild}.");
                DownloadPackagesForPush(hostBlob + forcePushBuild);
                return c.Success();
            }

            List<string> buildVersions = new List<string>();

            Regex buildVersionRegex = new Regex(@"Binaries/(?<version>\d+\.\d+\.\d+-[^-]+-\d{6})/$");

            foreach (string file in AzurePublisherTool.ListBlobs(hostBlob))
            {
                var match = buildVersionRegex.Match(file);
                if (match.Success)
                {
                    buildVersions.Add(match.Groups["version"].Value);
                }
            }

            // Sort decending
            buildVersions.Sort();
            buildVersions.Reverse();

            // Try to publish the last 10 builds
            foreach (var bv in buildVersions.Take(10))
            {
                Console.WriteLine($"Checking drop version: {bv}");

                if (ShouldDownloadAndPush(hostBlob, bv))
                {
                    DownloadPackagesForPush(hostBlob + bv);
                }
            }

            return c.Success();
        }

        private static bool ShouldDownloadAndPush(string hostBlob, string buildVersion)
        {
            // Set of runtime ids to look for to act as the signal that the build
            // as finished each of these legs of the build.
            Dictionary<string, bool> runtimes = new Dictionary<string, bool>()
            {
                {"win7-x64", false },
                {"win7-x86", false },
                {"osx.10.10-x64", false },
                {"rhel.7-x64", false },
                {"ubuntu.14.04-x64", false },
                {"debian.8-x64", false },
            };

            var buildFiles = AzurePublisherTool.ListBlobs(hostBlob + buildVersion);

            foreach (var bf in buildFiles)
            {
                string buildFile = Path.GetFileName(bf);

                if (buildFile == PackagePushedSemaphoreFileName)
                {
                    Console.WriteLine($"Found '{PackagePushedSemaphoreFileName}' for build version {buildVersion} so skipping this drop.");
                    // Nothing to do because the latest build is uploaded.
                    return false;
                }

                foreach (var runtime in runtimes.Keys)
                {
                    if (buildFile.StartsWith($"runtime.{runtime}"))
                    {
                        runtimes[runtime] = true;
                        break;
                    }
                }
            }

            bool missingRuntime = false;
            foreach (var runtime in runtimes)
            {
                if (!runtime.Value)
                {
                    missingRuntime = true;
                    Console.WriteLine($"Version {buildVersion} missing packages for runtime {runtime.Key}");
                }
            }

            if (missingRuntime)
                Console.WriteLine($"Build version {buildVersion} is missing some runtime packages so not pushing this drop.");

            return !missingRuntime;
        }

        private static void DownloadPackagesForPush(string pathToDownload)
        {
            AzurePublisherTool.DownloadFiles(pathToDownload, ".nupkg", Dirs.PackagesNoRID);

            string pushedSemaphore = Path.Combine(Dirs.PackagesNoRID, PackagePushedSemaphoreFileName);
            File.WriteAllText(pushedSemaphore, $"Packages pushed for build {pathToDownload}");
            AzurePublisherTool.PublishFile(pathToDownload + "/" + PackagePushedSemaphoreFileName, pushedSemaphore);
        }
    }
}
