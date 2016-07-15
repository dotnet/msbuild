using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class FinalizeBuild : Task
    {
        private AzurePublisher _azurePublisher;

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public string NugetVersion { get; set; }

        [Required]
        public string Channel { get; set; }

        [Required]
        public string CommitHash { get; set; }

        [Required]
        public string BranchName { get; set; }

        private AzurePublisher AzurePublisherTool
        {
            get
            {
                if(_azurePublisher == null)
                {
                    _azurePublisher = new AzurePublisher(AccountName, AccountKey);
                }

                return _azurePublisher;
            }
        }

        public override bool Execute()
        {
            DoFinalizeBuild();

            return true;
        }

        private void DoFinalizeBuild()
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
                    return;
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

                    string cliVersion = Utils.GetVersionFileContent(CommitHash, NugetVersion);
                    AzurePublisherTool.PublishStringToBlob($"{targetContainer}/latest.version", cliVersion);

                    UpdateVersionsRepo();
                }
                finally
                {
                    AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
                }
            }
        }

        private bool CheckIfAllBuildsHavePublished()
        {
            var badges = new Dictionary<string, bool>()
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

            IEnumerable<string> blobs = AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, NugetVersion);
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

        private void CopyBlobsToLatest(string destinationFolder)
        {
            foreach (string blob in AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, NugetVersion))
            {
                string targetName = Path.GetFileName(blob)
                                        .Replace(NugetVersion, "latest");

                string target = $"{destinationFolder}/{targetName}";
                AzurePublisherTool.CopyBlob(blob, target);
            }
        }

        private void UpdateVersionsRepo()
        {
            string githubAuthToken = EnvVars.EnsureVariable("GITHUB_PASSWORD");
            string nupkgFilePath = Dirs.Packages;
            string branchName = BranchName;
            string versionsRepoPath = $"build-info/dotnet/cli/{branchName}/Latest";

            VersionRepoUpdater repoUpdater = new VersionRepoUpdater(githubAuthToken);
            repoUpdater.UpdatePublishedVersions(nupkgFilePath, versionsRepoPath).Wait();
        }
    }
}