// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;

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
    /// DOTNET_VERSION_URL - The Url to the root of the version information (this is combined with the fragments below) (ex. "https://raw.githubusercontent.com/dotnet/versions/master/build-info")
    /// ROSLYN_VERSION_FRAGMENT - The fragment to combine with DOTNET_VERSION_URL to get the current dotnet/roslyn package versions. (ex. "dotnet/roslyn/netcore1.0")
    /// CORESETUP_VERSION_FRAGMENT - The fragment to combine with DOTNET_VERSION_URL to get the current dotnet/core-setup package versions. (ex. "dotnet/core-setup/master")
    /// GITHUB_ORIGIN_OWNER - The owner of the GitHub fork to push the commit and create the PR from. (ex. "dotnet-bot")
    /// GITHUB_UPSTREAM_OWNER - The owner of the GitHub base repo to create the PR to. (ex. "dotnet")
    /// GITHUB_PROJECT - The repo name under the ORIGIN and UPSTREAM owners. (ex. "cli")
    /// GITHUB_UPSTREAM_BRANCH - The branch in the GitHub base repo to create the PR to. (ex. "master");
    /// GITHUB_PULL_REQUEST_NOTIFICATIONS - A semi-colon ';' separated list of GitHub users to notify on the PR.
    /// </remarks>
    public class Config
    {
        public static Config Instance { get; } = new Config();

        private Lazy<string> _userName = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_USER"));
        private Lazy<string> _email = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_EMAIL"));
        private Lazy<string> _password = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_PASSWORD"));

        private Lazy<string> _dotNetVersionUrl = new Lazy<string>(() => GetEnvironmentVariable("DOTNET_VERSION_URL", "https://raw.githubusercontent.com/dotnet/versions/master/build-info"));

        private Lazy<string> _gitHubUpstreamOwner = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_UPSTREAM_OWNER", "dotnet"));
        private Lazy<string> _gitHubProject = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_PROJECT", "cli"));
        private Lazy<string> _gitHubUpstreamBranch = new Lazy<string>(() => GetEnvironmentVariable("GITHUB_UPSTREAM_BRANCH", GetDefaultUpstreamBranch()));
        private Lazy<string[]> _gitHubPullRequestNotifications = new Lazy<string[]>(() =>
                                                GetEnvironmentVariable("GITHUB_PULL_REQUEST_NOTIFICATIONS", "")
                                                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

        Lazy<Dictionary<string, string>> _versionFragments = new Lazy<Dictionary<string, string>>(() =>
                 System.Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().Where(entry => ((string)entry.Key).EndsWith("_VERSION_FRAGMENT")).ToDictionary<DictionaryEntry, string, string>(entry =>
                    ((string)entry.Key).Replace("_VERSION_FRAGMENT","").ToLowerInvariant(), entry => (string)entry.Value, StringComparer.OrdinalIgnoreCase));
        private Config()
        {
        }

        public string UserName => _userName.Value;
        public string Email => _email.Value;
        public string Password => _password.Value;
        public string DotNetVersionUrl => _dotNetVersionUrl.Value;
        public Dictionary<string, string> VersionFragments => _versionFragments.Value;
        public bool HasVersionFragment(string repoName) => _versionFragments.Value.ContainsKey(repoName);
        public string GitHubUpstreamOwner => _gitHubUpstreamOwner.Value;
        public string GitHubProject => _gitHubProject.Value;
        public string GitHubUpstreamBranch => _gitHubUpstreamBranch.Value;
        public string[] GitHubPullRequestNotifications => _gitHubPullRequestNotifications.Value;

        private static string GetEnvironmentVariable(string name, string defaultValue = null)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (value == null)
            {
                value = defaultValue;
            }

            if (value == null)
            {
                throw new InvalidOperationException($"Can't find environment variable '{name}'.");
            }

            return value;
        }

        private static string GetDefaultUpstreamBranch()
        {
            return GetRepoMSBuildPropValue("BranchInfo.props", "BranchName") ?? "master";
        }

        private static string GetDefaultCoreSetupVersionFragment()
        {
            // by default, the current core-setup branch should match the current cli branch name
            string coreSetupChannel = Instance.GitHubUpstreamBranch;

            return $"dotnet/core-setup/{coreSetupChannel}";
        }

        private static string GetRepoMSBuildPropValue(string propsFileName, string propertyName)
        {
            var propsFilePath = Path.Combine(Dirs.RepoRoot, "build", propsFileName);
            var root = XDocument.Load(propsFilePath).Root;
            var ns = root.Name.Namespace;

            var value = root
                .Elements(ns + "PropertyGroup")
                .Elements(ns + propertyName)
                .FirstOrDefault()
                ?.Value;

            if (string.IsNullOrEmpty(value))
            {
                Console.WriteLine($"Could not find a property named '{propertyName}' in {propsFilePath}");
                return null;
            }

            return value;
        }
    }
}
