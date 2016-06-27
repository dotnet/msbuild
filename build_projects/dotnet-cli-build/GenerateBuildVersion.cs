using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Security.Cryptography;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateBuildVersionInfo : Task
    {
        [Required]
        public string RepoRoot { get; set; }

        [Output]
        public ITaskItem OutputBuildVersionInfo { get; set; }

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

            OutputBuildVersionInfo = ConstructBuildVersionInfoItem(buildVersion, commitHash);

            return true;
        }

        private ITaskItem ConstructBuildVersionInfoItem(BuildVersion buildVersion, string commitHash)
        {
            var versionInfo = new TaskItem();
            versionInfo.ItemSpec = "BuildVersionInfo";

            versionInfo.SetMetadata("CommitHash", commitHash);
            versionInfo.SetMetadata("Major", buildVersion.Major.ToString());
            versionInfo.SetMetadata("Minor", buildVersion.Minor.ToString());
            versionInfo.SetMetadata("Patch", buildVersion.Patch.ToString());
            versionInfo.SetMetadata("ReleaseSuffix", buildVersion.ReleaseSuffix);
            versionInfo.SetMetadata("CommitCount", buildVersion.CommitCountString);
            versionInfo.SetMetadata("VersionSuffix", buildVersion.VersionSuffix);
            versionInfo.SetMetadata("SimpleVersion", buildVersion.SimpleVersion);
            versionInfo.SetMetadata("NugetVersion", buildVersion.NuGetVersion);
            versionInfo.SetMetadata("MsiVersion", buildVersion.GenerateMsiVersion());

            return versionInfo;
        }
    }
}
