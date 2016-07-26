// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateBuildVersionInfo : ToolTask
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

        private int _commitCount;

        public override bool Execute()
        {
            base.Execute();

            var branchInfo = new BranchInfo(RepoRoot);

            var buildVersion = new BuildVersion()
            {
                Major = int.Parse(branchInfo.Entries["MAJOR_VERSION"]),
                Minor = int.Parse(branchInfo.Entries["MINOR_VERSION"]),
                Patch = int.Parse(branchInfo.Entries["PATCH_VERSION"]),
                ReleaseSuffix = branchInfo.Entries["RELEASE_SUFFIX"],
                CommitCount = _commitCount
            };

            VersionMajor = buildVersion.Major;
            VersionMinor = buildVersion.Minor;
            VersionPatch = buildVersion.Patch;
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

        protected override string ToolName
        {
            get { return "git"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            return "git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"rev-list --count HEAD";
        }

        protected override void LogEventsFromTextOutput(string line, MessageImportance importance)
        {
            _commitCount = int.Parse(line);
        }
    }
}