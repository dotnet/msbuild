// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Build.Framework;
using Octokit;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Scripts
{
    /// <summary>
    /// Creates a GitHub Pull Request for the current changes in the repo.
    /// </summary>
    public static class PushPRTargets
    {
        private const string PullRequestTitle = "Updating dependencies from last known good builds";

        private static readonly Config s_config = Config.Instance;

        [Target(nameof(CommitChanges), nameof(CreatePR))]
        public static BuildTargetResult PushPR(BuildTargetContext c) => c.Success();

        /// <summary>
        /// Commits all the current changes in the repo and pushes the commit to a remote
        /// so a PR can be created for it.
        /// </summary>
        [Target]
        public static BuildTargetResult CommitChanges(BuildTargetContext c)
        {
            Cmd("git", "add", ".")
                .Execute()
                .EnsureSuccessful();

            string userName = s_config.UserName;
            string email = s_config.Email;

            Cmd("git", "commit", "-m", PullRequestTitle, "--author", $"{userName} <{email}>")
                .EnvironmentVariable("GIT_COMMITTER_NAME", userName)
                .EnvironmentVariable("GIT_COMMITTER_EMAIL", email)
                .Execute()
                .EnsureSuccessful();

            string remoteUrl = $"github.com/{s_config.GitHubOriginOwner}/{s_config.GitHubProject}.git";
            string remoteBranchName = $"UpdateDependencies{DateTime.UtcNow.ToString("yyyyMMddhhmmss")}";
            string refSpec = $"HEAD:refs/heads/{remoteBranchName}";

            string logMessage = $"git push https://{remoteUrl} {refSpec}";
            BuildReporter.BeginSection("EXEC", logMessage);

            CommandResult pushResult =
                Cmd("git", "push", $"https://{userName}:{s_config.Password}@{remoteUrl}", refSpec)
                    .QuietBuildReporter()  // we don't want secrets showing up in our logs
                    .CaptureStdErr() // git push will write to StdErr upon success, disable that
                    .CaptureStdOut()
                    .Execute();

            var message = logMessage + $" exited with {pushResult.ExitCode}";
            if (pushResult.ExitCode == 0)
            {
                BuildReporter.EndSection("EXEC", message.Green(), success: true);
            }
            else
            {
                BuildReporter.EndSection("EXEC", message.Red().Bold(), success: false);
            }

            pushResult.EnsureSuccessful(suppressOutput: true);

            c.SetRemoteBranchName(remoteBranchName);

            return c.Success();
        }

        /// <summary>
        /// Creates a GitHub PR for the remote branch created above.
        /// </summary>
        [Target]
        public static BuildTargetResult CreatePR(BuildTargetContext c)
        {
            string remoteBranchName = c.GetRemoteBranchName();

            NewPullRequest prInfo = new NewPullRequest(
                PullRequestTitle,
                s_config.GitHubOriginOwner + ":" + remoteBranchName,
                s_config.GitHubUpstreamBranch);

            GitHubClient gitHub = new GitHubClient(new ProductHeaderValue("dotnetDependencyUpdater"));

            gitHub.Credentials = new Credentials(s_config.Password);

            PullRequest createdPR = gitHub.PullRequest.Create(s_config.GitHubUpstreamOwner, s_config.GitHubProject, prInfo).Result;
            c.Info($"Created Pull Request: {createdPR.HtmlUrl}");

            return c.Success();
        }

        private static string GetRemoteBranchName(this BuildTargetContext c)
        {
            return (string)c.BuildContext["RemoteBranchName"];
        }

        private static void SetRemoteBranchName(this BuildTargetContext c, string value)
        {
            c.BuildContext["RemoteBranchName"] = value;
        }
    }
}
