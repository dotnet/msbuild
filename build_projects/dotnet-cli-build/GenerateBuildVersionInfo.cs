// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateBuildVersionInfo : Task
    {
        [Required]
        public string RepoRoot { get; set; }

        [Output]
        public int VersionMajor { get; set; }

        [Output]
        public int VersionMinor { get; set; }

        [Output]
        public int VersionPatch { get; set; }

        [Output]
        public string CommitHash { get; set; }

        [Output]
        public string CommitCount { get; set; }

        [Output]
        public string ReleaseSuffix { get; set; }

        [Output]
        public string VersionSuffix { get; set; }

        [Output]
        public string SimpleVersion { get; set; }

        [Output]
        public string NugetVersion { get; set; }

        [Output]
        public string MsiVersion { get; set; }

        [Output]
        public string VersionBadgeMoniker { get; set; }

        [Output]
        public string Channel { get; set; }

        [Output]
        public string BranchName { get; set; }

        public override bool Execute()
        {
            var branchInfo = new BranchInfo(RepoRoot);

            var commitCount = GitUtils.GetCommitCount();
            var commitHash = GitUtils.GetCommitHash();

            var buildVersion = new BuildVersion()
            {
                Major = int.Parse(branchInfo.Entries["MAJOR_VERSION"]),
                Minor = int.Parse(branchInfo.Entries["MINOR_VERSION"]),
                Patch = int.Parse(branchInfo.Entries["PATCH_VERSION"]),
                ReleaseSuffix = branchInfo.Entries["RELEASE_SUFFIX"],
                CommitCount = commitCount
            };

            VersionMajor = buildVersion.Major;
            VersionMinor = buildVersion.Minor;
            VersionPatch = buildVersion.Patch;
            CommitHash = commitHash;
            CommitCount = buildVersion.CommitCountString;
            ReleaseSuffix = buildVersion.ReleaseSuffix;
            VersionSuffix = buildVersion.VersionSuffix;
            SimpleVersion = buildVersion.SimpleVersion;
            NugetVersion = buildVersion.NuGetVersion;
            MsiVersion = buildVersion.GenerateMsiVersion();
            VersionBadgeMoniker = Monikers.GetBadgeMoniker();
            Channel = branchInfo.Entries["CHANNEL"];
            BranchName= branchInfo.Entries["BRANCH_NAME"];

            return true;
        }
    }
}