using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Build.Framework;

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
            SharedFrameworkNugetVersion = CliDependencyVersions.SharedFrameworkVersion;
            Channel = c.BuildContext.Get<string>("Channel");

            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init),
        nameof(PublishTargets.InitPublish),
        nameof(PublishTargets.PublishArtifacts),
        nameof(PublishTargets.FinalizeBuild))]
        [Environment("PUBLISH_TO_AZURE_BLOB", "1", "true")] // This is set by CI systems
        public static BuildTargetResult Publish(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult FinalizeBuild(BuildTargetContext c)
        {
            if (CheckIfAllBuildsHavePublished())
            {
                string targetContainer = $"{Channel}/Binaries/Latest/";
                string targetVersionFile = $"{targetContainer}{CliNuGetVersion}";
                string semaphoreBlob = $"{Channel}/Binaries/publishSemaphore";
                AzurePublisherTool.CreateBlobIfNotExists(semaphoreBlob);
                string leaseId = AzurePublisherTool.AcquireLeaseOnBlob(semaphoreBlob);

                // Prevent race conditions by dropping a version hint of what version this is. If we see this file
                // and it is the same as our version then we know that a race happened where two+ builds finished 
                // at the same time and someone already took care of publishing and we have no work to do.
                if (AzurePublisherTool.IsLatestSpecifiedVersion(targetVersionFile))
                {
                    AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
                    return c.Success();
                }
                else
                {
                    Regex versionFileRegex = new Regex(@"(?<version>\d\.\d\.\d)-(?<release>.*)?");

                    // Delete old version files
                    AzurePublisherTool.ListBlobs($"{targetContainer}")
                        .Select(s => s.Replace("/dotnet/", ""))
                        .Where(s => versionFileRegex.IsMatch(s))
                        .ToList()
                        .ForEach(f => AzurePublisherTool.TryDeleteBlob(f));

                    // Drop the version file signaling such for any race-condition builds (see above comment).
                    AzurePublisherTool.DropLatestSpecifiedVersion(targetVersionFile);
                }

                try
                {
                    // Copy the latest CLI bits
                    CopyBlobs($"{Channel}/Binaries/{CliNuGetVersion}/", targetContainer);

                    // Copy the latest installer files
                    CopyBlobs($"{Channel}/Installers/{CliNuGetVersion}/", $"{Channel}/Installers/Latest/");

                    // Generate the CLI and SDK Version text files
                    List<string> versionFiles = new List<string>()
                    {
                        "win.x86.version",
                        "win.x64.version",
                        "ubuntu.x64.version",
                        "ubuntu.16.04.x64.version",
                        "rhel.x64.version",
                        "osx.x64.version",
                        "debian.x64.version",
                        "centos.x64.version",
                        "fedora.23.x64.version",
                        "opensuse.13.2.x64.version"
                    };

                    string cliVersion = Utils.GetCliVersionFileContent(c);
                    foreach (string version in versionFiles)
                    {
                        AzurePublisherTool.PublishStringToBlob($"{Channel}/dnvm/latest.{version}", cliVersion);
                    }
                }
                finally
                {
                    AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
                }
            }

            return c.Success();
        }

        private static void CopyBlobs(string sourceFolder, string destinationFolder)
        {
            foreach (string blob in AzurePublisherTool.ListBlobs(sourceFolder))
            {
                string source = blob.Replace("/dotnet/", "");
                string targetName = Path.GetFileName(blob)
                                        .Replace(CliNuGetVersion, "latest");

                string target = $"{destinationFolder}{targetName}";
                AzurePublisherTool.CopyBlob(source, target);
            }
        }

        private static bool CheckIfAllBuildsHavePublished()
        {
            Dictionary<string, bool> badges = new Dictionary<string, bool>()
             {
                 { "Windows_x86", false },
                 { "Windows_x64", false },
                 { "Ubuntu_x64", false },
                 { "Ubuntu_16_04_x64", false },
                 { "RHEL_x64", false },
                 { "OSX_x64", false },
                 { "Debian_x64", false },
                 { "CentOS_x64", false },
                 { "Fedora_23_x64", false },
                 { "openSUSE_13_2_x64", false }
             };

            List<string> blobs = new List<string>(AzurePublisherTool.ListBlobs($"{Channel}/Binaries/{CliNuGetVersion}/"));

            var versionBadgeName = $"{Monikers.GetBadgeMoniker()}";
            if (badges.ContainsKey(versionBadgeName) == false)
            {
                throw new ArgumentException("A new OS build was added without adding the moniker to the {nameof(badges)} lookup");
            }

            foreach (string file in blobs)
            {
                string name = Path.GetFileName(file);
                string key = string.Empty;

                foreach (string img in badges.Keys)
                {
                    if ((name.StartsWith($"{img}")) && (name.EndsWith(".svg")))
                    {
                        key = img;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(key) == false)
                {
                    badges[key] = true;
                }
            }

            return badges.Keys.All(key => badges[key]);
        }

        [Target(
            nameof(PublishTargets.PublishInstallerFilesToAzure),
            nameof(PublishTargets.PublishArchivesToAzure),
            /*nameof(PublishTargets.PublishDebFilesToDebianRepo),*/ //https://github.com/dotnet/cli/issues/2973
            nameof(PublishTargets.PublishCliVersionBadge))]
        public static BuildTargetResult PublishArtifacts(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishTargets.PublishSdkInstallerFileToAzure),
            nameof(PublishTargets.PublishCombinedFrameworkSDKHostInstallerFileToAzure))]
        public static BuildTargetResult PublishInstallerFilesToAzure(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishTargets.PublishCombinedHostFrameworkSdkArchiveToAzure),
            nameof(PublishTargets.PublishCombinedFrameworkSDKArchiveToAzure),
            nameof(PublishTargets.PublishSDKSymbolsArchiveToAzure))]
        public static BuildTargetResult PublishArchivesToAzure(BuildTargetContext c) => c.Success();

        [Target(
            nameof(PublishSdkDebToDebianRepo))]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
        public static BuildTargetResult PublishDebFilesToDebianRepo(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCliVersionBadge(BuildTargetContext c)
        {
            var versionBadge = c.BuildContext.Get<string>("VersionBadge");
            var versionBadgeBlob = $"{Channel}/Binaries/{CliNuGetVersion}/{Path.GetFileName(versionBadge)}";
            AzurePublisherTool.PublishFile(versionBadgeBlob, versionBadge);
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
        public static BuildTargetResult PublishSdkInstallerFileToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");

            AzurePublisherTool.PublishInstallerFile(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows, BuildPlatform.OSX)]
        public static BuildTargetResult PublishCombinedFrameworkSDKHostInstallerFileToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var installerFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");

            AzurePublisherTool.PublishInstallerFile(installerFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult PublishCombinedFrameworkSDKArchiveToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkSDKCompressedFile");

            AzurePublisherTool.PublishArchive(archiveFile, Channel, version);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCombinedHostFrameworkSdkArchiveToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile");

            AzurePublisherTool.PublishArchive(archiveFile, Channel, version);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishSDKSymbolsArchiveToAzure(BuildTargetContext c)
        {
            var version = CliNuGetVersion;
            var archiveFile = c.BuildContext.Get<string>("SdkSymbolsCompressedFile");

            AzurePublisherTool.PublishArchive(archiveFile, Channel, version);

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
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

        [Target(nameof(PrepareTargets.Init))]
        public static BuildTargetResult UpdateVersionsRepo(BuildTargetContext c)
        {
            string githubAuthToken = EnvVars.EnsureVariable("GITHUB_PASSWORD");
            string nupkgFilePath = EnvVars.EnsureVariable("NUPKG_FILE_PATH");
            string versionsRepoPath = EnvVars.EnsureVariable("VERSIONS_REPO_PATH");

            VersionRepoUpdater repoUpdater = new VersionRepoUpdater(githubAuthToken);
            repoUpdater.UpdatePublishedVersions(nupkgFilePath, versionsRepoPath).Wait();

            return c.Success();
        }
    }
}

