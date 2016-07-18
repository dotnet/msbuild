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

            List<BuildInfo> buildInfos = new List<BuildInfo>();

            buildInfos.Add(BuildInfo.Get("CoreFx", s_config.CoreFxVersionUrl, fetchLatestReleaseFile: false));
            buildInfos.Add(BuildInfo.Get("CoreClr", s_config.CoreClrVersionUrl, fetchLatestReleaseFile: false));
            buildInfos.Add(BuildInfo.Get("Roslyn", s_config.RoslynVersionUrl, fetchLatestReleaseFile: false));
            buildInfos.Add(BuildInfo.Get("CoreSetup", s_config.CoreSetupVersionUrl, fetchLatestReleaseFile: false));

            IEnumerable<IDependencyUpdater> updaters = GetUpdaters();

            GitHubAuth gitHubAuth = new GitHubAuth(s_config.Password, s_config.UserName, s_config.Email);

            DependencyUpdater dependencyUpdater = new DependencyUpdater(
                gitHubAuth,
                s_config.GitHubProject,
                s_config.GitHubUpstreamOwner,
                s_config.GitHubUpstreamBranch,
                s_config.UserName,
                s_config.GitHubPullRequestNotifications);

            if (args.Length > 0 && string.Equals("--Update", args[0], StringComparison.OrdinalIgnoreCase))
            {
                dependencyUpdater.Update(updaters, buildInfos);
            }
            else
            {
                dependencyUpdater.UpdateAndSubmitPullRequestAsync(updaters, buildInfos);
            }
        }

        private static IEnumerable<IDependencyUpdater> GetUpdaters()
        {
            yield return CreateProjectJsonUpdater();

            yield return CreateRegexUpdater(@"build_projects\shared-build-targets-utils\DependencyVersions.cs", "CoreCLRVersion", "Microsoft.NETCore.Runtime.CoreCLR");
            yield return CreateRegexUpdater(@"build_projects\shared-build-targets-utils\DependencyVersions.cs", "JitVersion", "Microsoft.NETCore.Jit");

            yield return CreateRegexUpdater(@"build_projects\dotnet-cli-build\CliDependencyVersions.cs", "SharedFrameworkVersion", "Microsoft.NETCore.App");
            yield return CreateRegexUpdater(@"build_projects\dotnet-cli-build\CliDependencyVersions.cs", "HostFxrVersion", "Microsoft.NETCore.DotNetHostResolver");
            yield return CreateRegexUpdater(@"build_projects\dotnet-cli-build\CliDependencyVersions.cs", "SharedHostVersion", "Microsoft.NETCore.DotNetHost");
        }

        private static IDependencyUpdater CreateProjectJsonUpdater()
        {
            IEnumerable<string> projectJsonFiles = GetProjectJsonsToUpdate();

            return new ProjectJsonUpdater(projectJsonFiles)
            {
                SkipStableVersions = false
            };
        }

        private static IEnumerable<string> GetProjectJsonsToUpdate()
        {
            const string noUpdateFileName = ".noautoupdate";

            return Enumerable.Union(
                Directory.GetFiles(Dirs.RepoRoot, "project.json", SearchOption.AllDirectories),
                Directory.GetFiles(Path.Combine(Dirs.RepoRoot, @"src\dotnet\commands\dotnet-new"), "project.json.template", SearchOption.AllDirectories))
                .Where(p => !File.Exists(Path.Combine(Path.GetDirectoryName(p), noUpdateFileName)) &&
                    !Path.GetDirectoryName(p).EndsWith("CSharp_Web", StringComparison.Ordinal));
        }

        private static IDependencyUpdater CreateRegexUpdater(string repoRelativePath, string dependencyPropertyName, string packageId)
        {
            return new FileRegexPackageUpdater()
            {
                Path = Path.Combine(Dirs.RepoRoot, repoRelativePath),
                PackageId = packageId,
                Regex = new Regex($@"{dependencyPropertyName} = ""(?<version>.*)"";"),
                VersionGroupName = "version"
            };
        }
    }
}
