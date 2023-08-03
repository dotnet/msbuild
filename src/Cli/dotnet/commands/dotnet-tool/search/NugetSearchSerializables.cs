// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;

namespace Microsoft.DotNet.Tools.Tool.Search
{
    /// <summary>
    /// All fields are possibly null other than Id, Version, Tags, Authors, Versions
    /// </summary>
    internal class SearchResultPackage
    {
        public SearchResultPackage(
            PackageId id,
            string latestVersion,
            string description,
            string summary,
            IReadOnlyCollection<string> tags,
            IReadOnlyCollection<string> authors,
            int totalDownloads,
            bool verified,
            IReadOnlyCollection<SearchResultPackageVersion> versions)
        {
            Id = id;
            LatestVersion = latestVersion ?? throw new ArgumentNullException(nameof(latestVersion));
            Description = description;
            Summary = summary;
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            Authors = authors ?? throw new ArgumentNullException(nameof(authors));
            TotalDownloads = totalDownloads;
            Verified = verified;
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
        }

        public PackageId Id { get; }
        public string LatestVersion { get; }
        public string Description { get; }
        public string Summary { get; }
        public IReadOnlyCollection<string> Tags { get; }
        public IReadOnlyCollection<string> Authors { get; }
        public int TotalDownloads { get; }
        public bool Verified { get; }
        public IReadOnlyCollection<SearchResultPackageVersion> Versions { get; }
    }

    internal class SearchResultPackageVersion
    {
        public SearchResultPackageVersion(string version, int downloads)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Downloads = downloads;
        }

        public string Version { get; }
        public int Downloads { get; }
    }
}
