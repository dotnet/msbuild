// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class CheckIfAllBuildsHavePublished : Task
    {
        private AzurePublisher _azurePublisher;

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public string ContainerName { get; set; }

        [Required]
        public string NugetVersion { get; set; }

        [Required]
        public string VersionBadgeMoniker { get; set; }

        [Output]
        public string HaveAllBuildsPublished { get; set; }

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
            var badges = new Dictionary<string, bool>()
            {
                { "win_x86", false },
                { "win_x64", false },
                { "osx_x64", false },
                { "linux_x64", false },
                { "rhel.6_x64", false },
                { "linux_musl_x64", false },
                { "all_linux_distros_native_installer", false },
                { "linux_arm", false },
                { "linux_arm64", false },
                { "win_arm", false }
            };

            if (!badges.ContainsKey(VersionBadgeMoniker))
            {
                throw new ArgumentException($"A new OS build '{VersionBadgeMoniker}' was added without adding the moniker to the {nameof(badges)} lookup");
            }

            IEnumerable<string> blobs = AzurePublisherTool.ListBlobs(AzurePublisher.Product.Sdk, NugetVersion);
            foreach (string file in blobs)
            {
                string name = Path.GetFileName(file);
                foreach (string img in badges.Keys)
                {
                    if ((name.StartsWith($"{img}_")) && (name.EndsWith(".svg")))
                    {
                        badges[img] = true;
                        break;
                    }
                }
            }

            HaveAllBuildsPublished = badges.Values.All(v => v).ToString();

            return true;
        }
    }
}
