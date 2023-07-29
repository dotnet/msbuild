// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class PackageSourceLocation
    {
        public PackageSourceLocation(
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null,
            string[] sourceFeedOverrides = null,
            string[] additionalSourceFeeds = null)
        {
            NugetConfig = nugetConfig;
            RootConfigDirectory = rootConfigDirectory;
            // Overrides other feeds
            SourceFeedOverrides = ExpandLocalFeed(sourceFeedOverrides);
            // Feeds to be using in addition to config
            AdditionalSourceFeed = ExpandLocalFeed(additionalSourceFeeds);
        }

        public FilePath? NugetConfig { get; }
        public DirectoryPath? RootConfigDirectory { get; }
        public string[] SourceFeedOverrides { get; private set; }
        public string[] AdditionalSourceFeed { get; private set; }

        private string[] ExpandLocalFeed(string[] sourceFeedOverrides)
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

                return localFeedsThatIsRooted;
            }
            else
            {
                return Array.Empty<string>();
            }
        }
    }
}
