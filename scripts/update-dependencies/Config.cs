// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Scripts
{
    /// <summary>
    /// Holds the configuration information for the update-dependencies script.
    /// </summary>
    /// <remarks>
    /// The following Environment Variables are required by this script:
    /// 
    /// GITHUB_USER - The user to commit the changes as.
    /// GITHUB_EMAIL - The user's email to commit the changes as.
    /// GITHUB_PASSWORD - The password/personal access token of the GitHub user.
    ///
    /// The following Environment Variables can optionally be specified:
    /// 
    /// COREFX_VERSION_URL - The Url to get the current CoreFx version. (ex. "https://raw.githubusercontent.com/dotnet/versions/master/dotnet/corefx/release/1.0.0-rc2/LKG.txt")
    /// GITHUB_ORIGIN_OWNER - The owner of the GitHub fork to push the commit and create the PR from. (ex. "dotnet-bot")
    /// GITHUB_UPSTREAM_OWNER - The owner of the GitHub base repo to create the PR to. (ex. "dotnet")
    /// GITHUB_PROJECT - The repo name under the ORIGIN and UPSTREAM owners. (ex. "cli")
    /// GITHUB_UPSTREAM_BRANCH - The branch in the GitHub base repo to create the PR to. (ex. "rel/1.0.0")
    /// GITHUB_PULL_REQUEST_NOTIFICATIONS - A semi-colon ';' separated list of GitHub users to notify on the PR.
    /// </remarks>
    public class Config
    {
        public static Config Instance { get; } = Read();

        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string CoreFxVersionUrl { get; set; }
        public string GitHubOriginOwner { get; set; }
        public string GitHubUpstreamOwner { get; set; }
        public string GitHubProject { get; set; }
        public string GitHubUpstreamBranch { get; set; }
        public string[] GitHubPullRequestNotifications { get; set; }

        private static Config Read()
        {
            string userName = GetEnvironmentVariable("GITHUB_USER");

            return new Config
            {
                UserName = userName,
                Email = GetEnvironmentVariable("GITHUB_EMAIL"),
                Password = GetEnvironmentVariable("GITHUB_PASSWORD"),

                CoreFxVersionUrl = GetEnvironmentVariable("COREFX_VERSION_URL", "https://raw.githubusercontent.com/dotnet/versions/master/dotnet/corefx/release/1.0.0-rc2/LKG.txt"),
                GitHubOriginOwner = GetEnvironmentVariable("GITHUB_ORIGIN_OWNER", userName),
                GitHubUpstreamOwner = GetEnvironmentVariable("GITHUB_UPSTREAM_OWNER", "dotnet"),
                GitHubProject = GetEnvironmentVariable("GITHUB_PROJECT", "cli"),
                GitHubUpstreamBranch = GetEnvironmentVariable("GITHUB_UPSTREAM_BRANCH", "rel/1.0.0"),
                GitHubPullRequestNotifications = GetEnvironmentVariable("GITHUB_PULL_REQUEST_NOTIFICATIONS", "")
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            };
        }

        private static string GetEnvironmentVariable(string name, string defaultValue = null)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (value == null)
            {
                value = defaultValue;
            }

            if (value == null)
            {
                throw new BuildFailureException($"Can't find environment variable '{name}'.");
            }

            return value;
        }
    }
}
