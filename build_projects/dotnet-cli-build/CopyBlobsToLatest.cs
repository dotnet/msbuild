// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !SOURCE_BUILD
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Cli.Build
{
    public class CopyBlobsToLatest : Task
    {
        private AzurePublisher _azurePublisher;

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public string Channel { get; set; }

        [Required]
        public string CommitHash { get; set; }

        [Required]
        public string ContainerName { get; set; }

        [Required]
        public string NugetVersion { get; set; }

        public bool Coherent { get; set; }

        private AzurePublisher AzurePublisherTool
        {
            get
            {
                if (_azurePublisher == null)
                {
                    _azurePublisher = new AzurePublisher(AccountName, AccountKey, ContainerName);
                }

                return _azurePublisher;
            }
        }

        public override bool Execute()
        {
            string targetFolder = $"{AzurePublisher.Product.Sdk}/{Channel}";

            string targetVersionFile = $"{targetFolder}/{CommitHash}";
            string semaphoreBlob = $"{targetFolder}/publishSemaphore";
            AzurePublisherTool.CreateBlobIfNotExists(semaphoreBlob);
            string leaseId = AzurePublisherTool.AcquireLeaseOnBlob(semaphoreBlob);

            // Prevent race conditions by dropping a version hint of what version this is. If we see this file
            // and it is the same as our version then we know that a race happened where two+ builds finished 
            // at the same time and someone already took care of publishing and we have no work to do.
            if (AzurePublisherTool.IsLatestSpecifiedVersion(targetVersionFile))
            {
                AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
                return true;
            }
            else
            {
                Regex versionFileRegex = new Regex(@"(?<CommitHash>[\w\d]{40})");

                // Delete old version files
                AzurePublisherTool.ListBlobs(targetFolder)
                    .Where(s => versionFileRegex.IsMatch(s))
                    .ToList()
                    .ForEach(f => AzurePublisherTool.TryDeleteBlob(f));

                // Drop the version file signaling such for any race-condition builds (see above comment).
                AzurePublisherTool.DropLatestSpecifiedVersion(targetVersionFile);
            }

            try
            {
                CopyBlobs(targetFolder);

                string cliVersion = Utils.GetVersionFileContent(CommitHash, NugetVersion);
                AzurePublisherTool.PublishStringToBlob($"{targetFolder}/latest.version", cliVersion);
                if (Coherent == true)
                {
                    AzurePublisherTool.PublishStringToBlob($"{targetFolder}/latest.coherent.version", cliVersion);
                }
            }
            finally
            {
                AzurePublisherTool.ReleaseLeaseOnBlob(semaphoreBlob, leaseId);
            }

            return true;
        }

        private void CopyBlobs(string destinationFolder)
        {
            Log.LogMessage("Copying blobs to {0}/{1}", ContainerName, destinationFolder);

            foreach (string blob in AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, NugetVersion))
            {
                string targetName = Path.GetFileName(blob)
                                        .Replace(NugetVersion, "latest");

                string target = $"{destinationFolder}/{targetName}";

                AzurePublisherTool.CopyBlob(blob, target);
            }
        }
    }
}
#endif
