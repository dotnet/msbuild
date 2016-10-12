// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class UpdateVersionsRepo : Task
    {
        [Required]
        public string BranchName { get; set; }

        public override bool Execute()
        {
            string githubAuthToken = EnvVars.EnsureVariable("GITHUB_PASSWORD");
            string nupkgFilePath = Dirs.Packages;
            string branchName = BranchName;
            string versionsRepoPath = $"build-info/dotnet/cli/{branchName}/Latest";

            VersionRepoUpdater repoUpdater = new VersionRepoUpdater(githubAuthToken);
            repoUpdater.UpdatePublishedVersions(nupkgFilePath, versionsRepoPath).Wait();

            return true;
        }
    }
}
