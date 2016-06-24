using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class PublishTargets : Task
    {
        private static AzurePublisher AzurePublisherTool { get; set; }

        private static DebRepoPublisher DebRepoPublisherTool { get; set; }

        private static string Channel { get; set; }

        private static string CommitHash { get; set; }

        private static string CliNuGetVersion { get; set; }

        private static string SharedFrameworkNugetVersion { get; set; }

        public override bool Execute()
        {
            BuildContext context = new BuildSetup("MSBuild").UseAllTargetsFromAssembly<PublishTargets>().CreateBuildContext();
            BuildTargetContext c = new BuildTargetContext(context, null, null);

            return Publish(c).Success;
        }

        public static BuildTargetResult InitPublish(BuildTargetContext c)
        {
            AzurePublisherTool = new AzurePublisher();
            DebRepoPublisherTool = new DebRepoPublisher(Dirs.Packages);

            CliNuGetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            SharedFrameworkNugetVersion = CliDependencyVersions.SharedFrameworkVersion;
            Channel = c.BuildContext.Get<string>("Channel");
            CommitHash = c.BuildContext.Get<string>("CommitHash");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult Publish(BuildTargetContext c)
        {
            if (EnvVars.GetBool("PUBLISH_TO_AZURE_BLOB")) // This is set by CI systems
            {
                PrepareTargets.Init(c);

                InitPublish(c);
                PublishArtifacts(c);
                FinalizeBuild(c);
            }

            return c.Success();
        }

        public static BuildTargetResult FinalizeBuild(BuildTargetContext c)
        {
            if (CheckIfAllBuildsHavePublished())
            {
                string targetContainer = $"{AzurePublisher.Product.Sdk}/{Channel}";
                string targetVersionFile = $"{targetContainer}/{CommitHash}";
                string semaphoreBlob = $"{targetContainer}/publishSemaphore";
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
                    Regex versionFileRegex = new Regex(@"(?<CommitHash>[\w\d]{40})");

                    // Delete old version files
                    AzurePublisherTool.ListBlobs(targetContainer)
                        .Where(s => versionFileRegex.IsMatch(s))
                        .ToList()
                        .ForEach(f => AzurePublisherTool.TryDeleteBlob(f));

                    // Drop the version file signaling such for any race-condition builds (see above comment).
                    AzurePublisherTool.DropLatestSpecifiedVersion(targetVersionFile);
                }

                try
                {
                    CopyBlobsToLatest(targetContainer);

                    string cliVersion = Utils.GetCliVersionFileContent(c);
                    AzurePublisherTool.PublishStringToBlob($"{targetContainer}/latest.version", cliVersion);

                    UpdateVersionsRepo(c);
                }
                finally
                {
                    AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
                }
            }

            return c.Success();
        }

        private static void CopyBlobsToLatest(string destinationFolder)
        {
            foreach (string blob in AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, CliNuGetVersion))
            {
                string targetName = Path.GetFileName(blob)
                                        .Replace(CliNuGetVersion, "latest");

                string target = $"{destinationFolder}/{targetName}";
                AzurePublisherTool.CopyBlob(blob, target);
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

            var versionBadgeName = $"{Monikers.GetBadgeMoniker()}";
            if (!badges.ContainsKey(versionBadgeName))
            {
                throw new ArgumentException($"A new OS build '{versionBadgeName}' was added without adding the moniker to the {nameof(badges)} lookup");
            }

            IEnumerable<string> blobs = AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, CliNuGetVersion);
            foreach (string file in blobs)
            {
                string name = Path.GetFileName(file);
                foreach (string img in badges.Keys)
                {
                    if ((name.StartsWith($"{img}")) && (name.EndsWith(".svg")))
                    {
                        badges[img] = true;
                        break;
                    }
                }
            }

            return badges.Values.All(v => v);
        }

        public static BuildTargetResult PublishArtifacts(BuildTargetContext c)
        {
            PublishInstallerFilesToAzure(c);
            PublishArchivesToAzure(c);
            PublishDebFilesToDebianRepo(c);
            PublishCliVersionBadge(c);

            return c.Success();
        }

        public static BuildTargetResult PublishInstallerFilesToAzure(BuildTargetContext c)
        {
            PublishSdkInstallerFileToAzure(c);
            PublishCombinedFrameworkSDKHostInstallerFileToAzure(c);

            return c.Success();
        }

        public static BuildTargetResult PublishArchivesToAzure(BuildTargetContext c)
        {
            PublishCombinedHostFrameworkSdkArchiveToAzure(c);
            PublishCombinedFrameworkSDKArchiveToAzure(c);
            PublishSDKSymbolsArchiveToAzure(c);

            return c.Success();
        }

        public static BuildTargetResult PublishDebFilesToDebianRepo(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu))
            {
                PublishSdkDebToDebianRepo(c);
            }

            return c.Success();
        }

        public static BuildTargetResult PublishCliVersionBadge(BuildTargetContext c)
        {
            var versionBadge = c.BuildContext.Get<string>("VersionBadge");
            UploadFile(versionBadge);

            return c.Success();
        }

        public static BuildTargetResult PublishSdkInstallerFileToAzure(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu))
            {
                var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
                UploadFile(installerFile);
            }

            return c.Success();
        }

        public static BuildTargetResult PublishCombinedFrameworkSDKHostInstallerFileToAzure(BuildTargetContext c)
        {
            if (CurrentPlatform.IsAnyPlatform(BuildPlatform.Windows, BuildPlatform.OSX))
            {
                var installerFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");
                UploadFile(installerFile);
            }

            return c.Success();
        }

        public static BuildTargetResult PublishCombinedFrameworkSDKArchiveToAzure(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkSDKCompressedFile");
                UploadFile(archiveFile);
            }

            return c.Success();
        }

        public static BuildTargetResult PublishCombinedHostFrameworkSdkArchiveToAzure(BuildTargetContext c)
        {
            var archiveFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile");
            UploadFile(archiveFile);

            return c.Success();
        }

        public static BuildTargetResult PublishSDKSymbolsArchiveToAzure(BuildTargetContext c)
        {
            var archiveFile = c.BuildContext.Get<string>("SdkSymbolsCompressedFile");
            UploadFile(archiveFile);

            return c.Success();
        }

        public static BuildTargetResult PublishSdkDebToDebianRepo(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu))
            {
                var version = CliNuGetVersion;

                var packageName = CliMonikers.GetSdkDebianPackageName(c);
                var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
                var uploadUrl = AzurePublisher.CalculateFullUrlForFile(installerFile, AzurePublisher.Product.Sdk, version);

                DebRepoPublisherTool.PublishDebFileToDebianRepo(
                    packageName,
                    version,
                    uploadUrl);
            }

            return c.Success();
        }

        private static void UpdateVersionsRepo(BuildTargetContext c)
        {
            string githubAuthToken = EnvVars.EnsureVariable("GITHUB_PASSWORD");
            string nupkgFilePath = Dirs.Packages;
            string branchName = c.BuildContext.Get<string>("BranchName");
            string versionsRepoPath = $"build-info/dotnet/cli/{branchName}/Latest";

            VersionRepoUpdater repoUpdater = new VersionRepoUpdater(githubAuthToken);
            repoUpdater.UpdatePublishedVersions(nupkgFilePath, versionsRepoPath).Wait();
        }

        private static string UploadFile(string file)
        {
            return AzurePublisherTool.UploadFile(file, AzurePublisher.Product.Sdk, CliNuGetVersion);
        }
    }
}

