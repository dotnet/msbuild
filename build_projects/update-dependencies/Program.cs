// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Dependencies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Scripts
{
    public class Program
    {
        private static readonly Config s_config = Config.Instance;

        public static void Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            bool onlyUpdate = args.Length > 0 && string.Equals("--Update", args[0], StringComparison.OrdinalIgnoreCase);

            List<BuildInfo> buildInfos = new List<BuildInfo>();

            buildInfos.Add(GetBuildInfo("Roslyn", s_config.RoslynVersionFragment, fetchLatestReleaseFile: false));
            buildInfos.Add(GetBuildInfo("CoreSetup", s_config.CoreSetupVersionFragment, fetchLatestReleaseFile: false));

            IEnumerable<IDependencyUpdater> updaters = GetUpdaters();
            var dependencyBuildInfos = buildInfos.Select(buildInfo =>
                new DependencyBuildInfo(
                    buildInfo,
                    upgradeStableVersions: true,
                    disabledPackages: Enumerable.Empty<string>()));
            DependencyUpdateResults updateResults = DependencyUpdateUtils.Update(updaters, dependencyBuildInfos);

            if (!onlyUpdate && updateResults.ChangesDetected())
            {
                GitHubAuth gitHubAuth = new GitHubAuth(s_config.Password, s_config.UserName, s_config.Email);
                GitHubProject origin = new GitHubProject(s_config.GitHubProject, s_config.UserName);
                GitHubBranch upstreamBranch = new GitHubBranch(
                    s_config.GitHubUpstreamBranch,
                    new GitHubProject(s_config.GitHubProject, s_config.GitHubUpstreamOwner));

                string suggestedMessage = updateResults.GetSuggestedCommitMessage();
                string body = string.Empty;
                if (s_config.GitHubPullRequestNotifications.Any())
                {
                    body += PullRequestCreator.NotificationString(s_config.GitHubPullRequestNotifications);
                }

                new PullRequestCreator(gitHubAuth, origin, upstreamBranch)
                    .CreateOrUpdateAsync(
                        suggestedMessage,
                        suggestedMessage + $" ({upstreamBranch.Name})",
                        body)
                    .Wait();
            }
        }

        private static BuildInfo GetBuildInfo(string name, string buildInfoFragment, bool fetchLatestReleaseFile = true)
        {
            const string FileUrlProtocol = "file://";

            if (s_config.DotNetVersionUrl.StartsWith(FileUrlProtocol, StringComparison.Ordinal))
            {
                return BuildInfo.LocalFileGetAsync(
                           name,
                           s_config.DotNetVersionUrl.Substring(FileUrlProtocol.Length),
                           buildInfoFragment.Replace('/', Path.DirectorySeparatorChar),
                           fetchLatestReleaseFile)
                       .Result;
            }
            else
            {
                return BuildInfo.Get(name, $"{s_config.DotNetVersionUrl}/{buildInfoFragment}", fetchLatestReleaseFile);
            }
        }

        private static IEnumerable<IDependencyUpdater> GetUpdaters()
        {
            string dependencyVersionsPath = Path.Combine("build", "DependencyVersions.props");
            yield return CreateRegexUpdater(dependencyVersionsPath, "CLI_SharedFrameworkVersion", "Microsoft.NETCore.App");
            yield return CreateRegexUpdater(dependencyVersionsPath, "PlatformAbstractionsVersion", "Microsoft.DotNet.PlatformAbstractions");
            yield return CreateRegexUpdater(dependencyVersionsPath, "DependencyModelVersion", "Microsoft.Extensions.DependencyModel");
        }

        private static IDependencyUpdater CreateRegexUpdater(string repoRelativePath, string propertyName, string packageId)
        {
            return new FileRegexPackageUpdater()
            {
                Path = Path.Combine(Dirs.RepoRoot, repoRelativePath),
                PackageId = packageId,
                Regex = new Regex($@"<{propertyName}>(?<version>.*)</{propertyName}>"),
                VersionGroupName = "version"
            };
        }
    }
}
