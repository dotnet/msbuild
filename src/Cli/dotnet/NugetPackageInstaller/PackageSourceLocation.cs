// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class PackageSourceLocation
    {
        public PackageSourceLocation(
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null,
            string[] sourceFeedOverrides = null)
        {
            NugetConfig = nugetConfig;
            RootConfigDirectory = rootConfigDirectory;
            ExpandLocalFeedAndAssign(sourceFeedOverrides);
        }

        public FilePath? NugetConfig { get; }
        public DirectoryPath? RootConfigDirectory { get; }
        public string[] SourceFeedOverrides { get; private set; }

        private void ExpandLocalFeedAndAssign(string[] sourceFeedOverrides)
        {
            if (sourceFeedOverrides != null)
            {
                string[] localFeedsThatIsRooted = new string[sourceFeedOverrides.Length];
                for (int index = 0; index < sourceFeedOverrides.Length; index++)
                {
                    string feed = sourceFeedOverrides[index];
                    if (!Uri.IsWellFormedUriString(feed, UriKind.Absolute) && !Path.IsPathRooted(feed))
                    {
                        localFeedsThatIsRooted[index] = Path.GetFullPath(feed);
                    }
                    else
                    {
                        localFeedsThatIsRooted[index] = feed;
                    }
                }

                SourceFeedOverrides = localFeedsThatIsRooted;
            }
            else
            {
                SourceFeedOverrides = Array.Empty<string>();
            }
        }
    }
}
