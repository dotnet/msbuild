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

            HaveAllBuildsPublished = badges.Values.All(v => v).ToString();

            return true;
        }
    }
}
