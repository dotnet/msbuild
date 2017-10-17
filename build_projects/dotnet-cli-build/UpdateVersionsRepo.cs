// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

#if !SOURCE_BUILD
using Microsoft.DotNet.VersionTools.Automation;
#endif

namespace Microsoft.DotNet.Cli.Build
{
    public class UpdateVersionsRepo : Task
    {
        [Required]
        public string BranchName { get; set; }

        [Required]
        public string PackagesDirectory { get; set; }

        [Required]
        public string GitHubPassword { get; set; }

        public override bool Execute()
        {
#if !SOURCE_BUILD
            string versionsRepoPath = $"build-info/dotnet/cli/{BranchName}";

            GitHubAuth auth = new GitHubAuth(GitHubPassword);
            GitHubVersionsRepoUpdater repoUpdater = new GitHubVersionsRepoUpdater(auth);
            repoUpdater.UpdateBuildInfoAsync(
                Directory.GetFiles(PackagesDirectory, "*.nupkg"),
                versionsRepoPath).Wait();
#endif

            return true;
        }
    }
}
